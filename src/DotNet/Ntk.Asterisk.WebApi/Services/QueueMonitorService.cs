using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public interface IQueueMonitorService
{
    Task<IReadOnlyList<QueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default);
    Task<QueueDto?> GetQueueAsync(string name, CancellationToken cancellationToken = default);
    Task PauseMemberAsync(QueueMemberPauseRequest request, bool paused, CancellationToken cancellationToken = default);
    Task HangupEntryAsync(string channel, CancellationToken cancellationToken = default);
}

public sealed class QueueMonitorService : IQueueMonitorService, IHostedService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan EventDebounce = TimeSpan.FromMilliseconds(400);

    private readonly IAmiSession _ami;
    private readonly IAsteriskSettingsService _settings;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly IQueueAclStore _acl;
    private readonly IQueueAclConnectionRegistry _aclConnections;
    private readonly ILogger<QueueMonitorService> _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly object _debounceGate = new();
    private CancellationTokenSource? _debounceCts;
    private PeriodicTimer? _pollTimer;
    private CancellationTokenSource? _pollCts;
    private Task? _pollLoop;
    private volatile IReadOnlyList<QueueDto> _cache = Array.Empty<QueueDto>();

    public QueueMonitorService(
        IAmiSession ami,
        IAsteriskSettingsService settings,
        IHubContext<AsteriskHub> hub,
        IQueueAclStore acl,
        IQueueAclConnectionRegistry aclConnections,
        ILogger<QueueMonitorService> logger)
    {
        _ami = ami;
        _settings = settings;
        _hub = hub;
        _acl = acl;
        _aclConnections = aclConnections;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent += OnAmiEvent;
        _ami.ConnectionChanged += OnConnectionChanged;
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollTimer = new PeriodicTimer(PollInterval);
        _pollLoop = RunPollLoopAsync(_pollCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent -= OnAmiEvent;
        _ami.ConnectionChanged -= OnConnectionChanged;
        if (_pollCts is not null)
            await _pollCts.CancelAsync().ConfigureAwait(false);
        if (_pollLoop is not null)
        {
            try { await _pollLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        lock (_debounceGate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollCts?.Dispose();
        _publishLock.Dispose();
        lock (_debounceGate)
        {
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }

    public async Task<IReadOnlyList<QueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        var list = await FetchQueuesAsync(cancellationToken).ConfigureAwait(false);
        _cache = list;
        return list;
    }

    public async Task<QueueDto?> GetQueueAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var list = await GetQueuesAsync(cancellationToken).ConfigureAwait(false);
        return list.FirstOrDefault(q =>
            string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(q.RealName, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task PauseMemberAsync(
        QueueMemberPauseRequest request,
        bool paused,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Interface))
            throw new ArgumentException("Interface is required.", nameof(request));

        var action = new QueuePauseAction(request.Interface.Trim(), paused);
        if (!string.IsNullOrWhiteSpace(request.Queue))
            action.Queue = request.Queue.Trim();
        if (!string.IsNullOrWhiteSpace(request.Reason))
            action.Reason = request.Reason.Trim();

        var resp = await _ami.SendActionAsync(action, cancellationToken).ConfigureAwait(false);
        if (resp is null || !resp.IsSuccess())
            throw new InvalidOperationException(resp?.Message ?? "QueuePause failed");

        SchedulePublish();
    }

    public async Task HangupEntryAsync(string channel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel is required.", nameof(channel));

        var resp = await _ami.SendActionAsync(new HangupAction(channel.Trim()), cancellationToken)
            .ConfigureAwait(false);
        if (resp is null || !resp.IsSuccess())
            throw new InvalidOperationException(resp?.Message ?? "Hangup failed");

        SchedulePublish();
    }

    private async Task RunPollLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _pollTimer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (_ami.Connection?.IsConnected() != true)
                    continue;
                try
                {
                    await PublishSnapshotAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Queue poll failed");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown
        }
    }

    private void OnConnectionChanged(object? sender, EventArgs e) => SchedulePublish();

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        if (e is null)
            return;
        var type = e.GetType().Name;
        if (type.Contains("Queue", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Agent", StringComparison.OrdinalIgnoreCase))
        {
            SchedulePublish();
        }
    }

    private void SchedulePublish()
    {
        CancellationTokenSource cts;
        lock (_debounceGate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            cts = _debounceCts;
        }

        _ = DebouncedPublishAsync(cts.Token);
    }

    private async Task DebouncedPublishAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(EventDebounce, ct).ConfigureAwait(false);
            await PublishSnapshotAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private async Task PublishSnapshotAsync(CancellationToken ct)
    {
        await _publishLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ami.Connection?.IsConnected() != true)
                return;

            var list = await FetchQueuesAsync(ct).ConfigureAwait(false);
            _cache = list;
            await PublishFilteredAsync(list, ct).ConfigureAwait(false);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task PublishFilteredAsync(IReadOnlyList<QueueDto> list, CancellationToken ct)
    {
        var gate = _acl.IsGateActive;
        if (!gate)
        {
            await _hub.Clients.Group("monitor").SendAsync("queuesUpdated", list, ct).ConfigureAwait(false);
            await _hub.Clients.Group("queues").SendAsync("queuesUpdated", list, ct).ConfigureAwait(false);
            return;
        }

        foreach (var (connectionId, principal) in _aclConnections.Snapshot())
        {
            var filtered = QueueAclFilter.Apply(list, principal, gateActive: true);
            await _hub.Clients.Client(connectionId).SendAsync("queuesUpdated", filtered, ct).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<QueueDto>> FetchQueuesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var events = await _ami
                .SendEventGeneratingActionAsync(new QueueStatusAction(), 12_000, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            var nowEpoch = now.ToUnixTimeSeconds();
            var map = new ConcurrentDictionary<string, QueueBuilder>(StringComparer.OrdinalIgnoreCase);

            foreach (var ev in events.Events)
            {
                switch (ev)
                {
                    case QueueParamsEvent p:
                    {
                        var name = p.Queue ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                            break;
                        var b = map.GetOrAdd(name, n => new QueueBuilder(n));
                        b.ApplyParams(p, AttrInt(ev, "TalkTime"));
                        break;
                    }
                    case QueueMemberEvent m:
                    {
                        var name = m.Queue ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                            break;
                        var b = map.GetOrAdd(name, n => new QueueBuilder(n));
                        b.Members.Add(MapMember(m, nowEpoch));
                        break;
                    }
                    case QueueEntryEvent e:
                    {
                        var name = e.Queue ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                            break;
                        var b = map.GetOrAdd(name, n => new QueueBuilder(n));
                        b.Entries.Add(MapEntry(e));
                        break;
                    }
                }
            }

            return map.Values
                .Select(b => b.Build(now))
                .Select(ApplyDisplay)
                .Where(q => q is not null)
                .Cast<QueueDto>()
                .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QueueStatus failed");
            throw;
        }
    }

    private QueueDto? ApplyDisplay(QueueDto raw)
    {
        var opt = _settings.GetEffective();
        var hide = ParseNameSet(opt.QueueHideList);
        var show = ParseNameSet(opt.QueueShowList);
        var rename = ParseRenameMap(opt.QueueRenameMap);
        var real = raw.Name;

        if (show.Count > 0)
        {
            if (!show.Contains(real))
                return null;
        }
        else if (hide.Count > 0 && hide.Contains(real))
        {
            return null;
        }

        var display = rename.TryGetValue(real, out var renamed) && !string.IsNullOrWhiteSpace(renamed)
            ? renamed.Trim()
            : real;

        return new QueueDto
        {
            Name = display,
            RealName = real,
            Strategy = raw.Strategy,
            Max = raw.Max,
            CallsWaiting = raw.CallsWaiting,
            HoldtimeSeconds = raw.HoldtimeSeconds,
            TalkTimeSeconds = raw.TalkTimeSeconds,
            Completed = raw.Completed,
            Abandoned = raw.Abandoned,
            ServiceLevelSeconds = raw.ServiceLevelSeconds,
            ServiceLevelPerf = raw.ServiceLevelPerf,
            AbandonedPercent = raw.AbandonedPercent,
            Weight = raw.Weight,
            Members = raw.Members,
            Entries = raw.Entries,
            SnapshotUtc = raw.SnapshotUtc
        };
    }

    private static HashSet<string> ParseNameSet(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return set;
        foreach (var part in raw.Split(new[] { ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            if (t.Length > 0)
                set.Add(t);
        }
        return set;
    }

    private static Dictionary<string, string> ParseRenameMap(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return map;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(trimmed);
                if (json != null)
                {
                    foreach (var kv in json)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                            map[kv.Key.Trim()] = kv.Value.Trim();
                    }
                    return map;
                }
            }
            catch
            {
                // fall through to line parser
            }
        }

        foreach (var line in trimmed.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            if (key.Length > 0 && val.Length > 0)
                map[key] = val;
        }
        return map;
    }

    private static QueueMemberDto MapMember(QueueMemberEvent m, long nowEpoch)
    {
        var iface = FirstNonEmpty(
            m.Location,
            Attr(m, "Interface"),
            m.Name,
            m.MemberName) ?? string.Empty;

        var stateIface = FirstNonEmpty(Attr(m, "StateInterface"), iface);
        var status = m.Status;
        if (m.InCall && status is 1 or 2)
            status = 10;

        var lastCallAgo = m.LastCall > 0 ? (int)Math.Max(0, nowEpoch - m.LastCall) : (int?)null;
        var lastPauseEpoch = AttrLong(m, "LastPause");
        var lastPauseAgo = lastPauseEpoch > 0 ? (int)Math.Max(0, nowEpoch - lastPauseEpoch) : (int?)null;

        return new QueueMemberDto
        {
            Interface = iface,
            StateInterface = stateIface,
            Name = FirstNonEmpty(m.MemberName, m.Name, iface),
            Membership = m.Membership,
            Penalty = m.Penalty,
            CallsTaken = m.CallsTaken,
            LastCallEpoch = m.LastCall,
            LastCallAgoSeconds = lastCallAgo,
            Status = status,
            StatusLabel = StatusLabel(status, m.Paused, m.InCall),
            Paused = m.Paused,
            PausedReason = m.PausedReason,
            InCall = m.InCall,
            LastPauseEpoch = lastPauseEpoch,
            LastPauseAgoSeconds = lastPauseAgo
        };
    }

    private static QueueEntryDto MapEntry(QueueEntryEvent e)
    {
        var channel = FirstNonEmpty(e.Channel, Attr(e, "Channel"));
        var uniqueId = FirstNonEmpty(e.UniqueId, Attr(e, "Uniqueid"), Attr(e, "UniqueId"));
        var priority = AttrInt(e, "Priority");

        return new QueueEntryDto
        {
            Position = e.Position,
            Channel = channel,
            UniqueId = uniqueId,
            CallerId = e.CallerId,
            CallerIdName = e.CallerIdName,
            WaitSeconds = e.Wait,
            Priority = priority
        };
    }

    private static string StatusLabel(int status, bool paused, bool inCall)
    {
        if (paused)
            return "Paused";
        return status switch
        {
            0 => "Unknown",
            1 => "Not in use",
            2 => "In use",
            3 => "Busy",
            4 => "Invalid",
            5 => "Unavailable",
            6 => "Ringing",
            7 => "Ring+InUse",
            8 => "On Hold",
            10 => "In call",
            _ => inCall ? "In call" : $"Status {status}"
        };
    }

    private static string? Attr(ManagerEvent e, string key)
    {
        if (e.Attributes is null)
            return null;
        foreach (var kv in e.Attributes)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        return null;
    }

    private static int AttrInt(ManagerEvent e, string key)
    {
        var s = Attr(e, key);
        return int.TryParse(s, out var v) ? v : 0;
    }

    private static long AttrLong(ManagerEvent e, string key)
    {
        var s = Attr(e, key);
        return long.TryParse(s, out var v) ? v : 0;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    private sealed class QueueBuilder
    {
        private readonly string _name;
        public List<QueueMemberDto> Members { get; } = new();
        public List<QueueEntryDto> Entries { get; } = new();

        private string? _strategy;
        private int _max;
        private int _calls;
        private int _holdtime;
        private int _talkTime;
        private int _completed;
        private int _abandoned;
        private int _serviceLevel;
        private double _serviceLevelPerf;
        private int _weight;

        public QueueBuilder(string name) => _name = name;

        public void ApplyParams(QueueParamsEvent p, int talkTime)
        {
            _strategy = p.Strategy;
            _max = p.Max;
            _calls = p.Calls;
            _holdtime = p.Holdtime;
            _talkTime = talkTime;
            _completed = p.Completed;
            _abandoned = p.Abandoned;
            _serviceLevel = p.ServiceLevel;
            _serviceLevelPerf = p.ServiceLevelPerf;
            _weight = p.Weight;
        }

        public QueueDto Build(DateTimeOffset now)
        {
            var denom = _completed + _abandoned;
            var abandonedPct = denom > 0 ? Math.Round(100.0 * _abandoned / denom, 2) : 0;

            return new QueueDto
            {
                Name = _name,
                RealName = _name,
                Strategy = _strategy,
                Max = _max,
                CallsWaiting = _calls,
                HoldtimeSeconds = _holdtime,
                TalkTimeSeconds = _talkTime,
                Completed = _completed,
                Abandoned = _abandoned,
                ServiceLevelSeconds = _serviceLevel,
                ServiceLevelPerf = _serviceLevelPerf,
                AbandonedPercent = abandonedPct,
                Weight = _weight,
                Members = Members
                    .OrderBy(m => m.Name ?? m.Interface, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Entries = Entries.OrderBy(e => e.Position).ToList(),
                SnapshotUtc = now
            };
        }
    }
}
