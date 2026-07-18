using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public interface IMonitorService
{
    Task<IReadOnlyList<PeerDto>> GetPeersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerDto>> GetTrunksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default);
    ConfigVisibilityDto GetConfigVisibility();
}

public sealed class MonitorService : IMonitorService, IHostedService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan EventDebounce = TimeSpan.FromMilliseconds(250);

    private readonly IAmiSession _ami;
    private readonly IAsteriskSettingsService _settings;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<MonitorService> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _peerLastActivity =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _deviceStates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly object _debounceGate = new();
    private CancellationTokenSource? _debounceCts;
    private PeriodicTimer? _pollTimer;
    private CancellationTokenSource? _pollCts;
    private Task? _pollLoop;

    public MonitorService(
        IAmiSession ami,
        IAsteriskSettingsService settings,
        IHubContext<AsteriskHub> hub,
        ILogger<MonitorService> logger)
    {
        _ami = ami;
        _settings = settings;
        _hub = hub;
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

    public ConfigVisibilityDto GetConfigVisibility() => _settings.GetSiteSettings();

    public async Task<IReadOnlyList<PeerDto>> GetPeersAsync(CancellationToken cancellationToken = default)
    {
        var rawPeers = await FetchPeersRawAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChannelDto> channels;
        try
        {
            channels = await FetchChannelsRawAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            channels = Array.Empty<ChannelDto>();
        }

        return EnrichPeersWithLiveCalls(rawPeers, channels);
    }

    public async Task<IReadOnlyList<PeerDto>> GetTrunksAsync(CancellationToken cancellationToken = default)
    {
        var peers = await GetPeersAsync(cancellationToken).ConfigureAwait(false);
        return peers.Where(p => p.IsTrunk).ToList();
    }

    public Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default) =>
        FetchChannelsRawAsync(cancellationToken);

    private async Task<(IReadOnlyList<PeerDto> Peers, IReadOnlyList<ChannelDto> Channels)> BuildSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var rawPeers = await FetchPeersRawAsync(cancellationToken).ConfigureAwait(false);
        var channels = await FetchChannelsRawAsync(cancellationToken).ConfigureAwait(false);
        var peers = EnrichPeersWithLiveCalls(rawPeers, channels);
        return (peers, channels);
    }

    private async Task<IReadOnlyList<PeerDto>> FetchPeersRawAsync(CancellationToken cancellationToken)
    {
        try
        {
            var events = await _ami.SendEventGeneratingActionAsync(new SIPPeersAction(), null, cancellationToken)
                .ConfigureAwait(false);
            return events.Events
                .OfType<PeerEntryEvent>()
                .Select(MapPeerBase)
                .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SIPPeers failed");
            throw;
        }
    }

    private async Task<IReadOnlyList<ChannelDto>> FetchChannelsRawAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var events = await _ami.SendEventGeneratingActionAsync(new StatusAction(), null, cancellationToken)
                .ConfigureAwait(false);
            return events.Events
                .OfType<StatusEvent>()
                .Select(e => new ChannelDto
                {
                    Channel = e.Channel ?? string.Empty,
                    UniqueId = e.UniqueId,
                    State = e.State,
                    CallerId = e.CallerIdNum ?? e.CallerId,
                    Exten = e.Extension,
                    Context = e.Context,
                    Application = null,
                    DurationSeconds = e.Seconds >= 0 ? e.Seconds : null,
                    LastActivityUtc = now
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Channel))
                .OrderBy(c => c.Channel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StatusAction failed");
            throw;
        }
    }

    private PeerDto MapPeerBase(PeerEntryEvent e)
    {
        var id = e.ObjectName ?? string.Empty;
        var filter = _settings.GetEffective().TrunkPeerFilter;
        var isTrunk = false;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try { isTrunk = Regex.IsMatch(id, filter, RegexOptions.IgnoreCase); }
            catch { isTrunk = id.Contains("trunk", StringComparison.OrdinalIgnoreCase); }
        }
        else
        {
            isTrunk = id.Contains("trunk", StringComparison.OrdinalIgnoreCase);
        }

        var key = NormalizePeerKey(id);
        _peerLastActivity.TryGetValue(key, out var last);

        return new PeerDto
        {
            Id = id,
            Tech = e.ChannelType ?? _settings.GetEffective().ChannelTech,
            Status = e.Status ?? string.Empty,
            Ip = e.IpAddress,
            Channel = null,
            IsTrunk = isTrunk,
            LastActivityUtc = last == default ? null : last
        };
    }

    private IReadOnlyList<PeerDto> EnrichPeersWithLiveCalls(
        IReadOnlyList<PeerDto> peers,
        IReadOnlyList<ChannelDto> channels)
    {
        var now = DateTimeOffset.UtcNow;
        return peers.Select(p =>
        {
            var matched = channels.Where(c => PeerMatchesChannel(p, c)).ToList();
            var best = PickBestChannel(matched);
            _deviceStates.TryGetValue(NormalizePeerKey(p.Id), out var deviceState);
            // Also try tech/id keys stored from DeviceStateChange
            if (deviceState is null)
            {
                foreach (var k in DeviceKeysForPeer(p))
                {
                    if (_deviceStates.TryGetValue(k, out deviceState))
                        break;
                }
            }

            var callState = DeriveCallState(best, deviceState);
            var inCall = IsActiveCallState(callState);
            var duration = best?.DurationSeconds;
            var displayStatus = inCall
                ? FormatCallDisplayStatus(callState!, duration)
                : p.Status;

            DateTimeOffset? activity = p.LastActivityUtc;
            if (inCall)
                activity = now;

            return new PeerDto
            {
                Id = p.Id,
                Tech = p.Tech,
                Status = displayStatus,
                Ip = p.Ip,
                Channel = best?.Channel,
                IsTrunk = p.IsTrunk,
                LastActivityUtc = activity,
                CallState = callState,
                CallDurationSeconds = inCall ? duration : null,
                CallerId = best?.CallerId,
                InCall = inCall
            };
        }).ToList();
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        var schedule = false;

        if (e is PeerStatusEvent peerStatus)
        {
            RememberPeerActivity(peerStatus.Peer, DateTimeOffset.UtcNow);
            schedule = true;
        }

        if (e is DeviceStateChangeEvent deviceState)
        {
            var device = ResolveDeviceName(deviceState);
            var state = deviceState.Status
                ?? deviceState.Attributes?.GetValueOrDefault("State")
                ?? deviceState.Attributes?.GetValueOrDefault("DeviceState");
            if (!string.IsNullOrWhiteSpace(device) && !string.IsNullOrWhiteSpace(state))
            {
                var key = NormalizePeerKey(device);
                _deviceStates[key] = state!;
                RememberPeerActivity(key, DateTimeOffset.UtcNow);
                schedule = true;
            }
        }

        if (e is ExtensionStatusEvent extStatus)
        {
            var exten = extStatus.Exten;
            if (!string.IsNullOrWhiteSpace(exten))
            {
                _deviceStates[NormalizePeerKey(exten)] = MapExtensionStatusCode(extStatus.Status);
                RememberPeerActivity(exten, DateTimeOffset.UtcNow);
                schedule = true;
            }
        }

        if (e is NewChannelEvent or HangupEvent or BridgeEvent or NewStateEvent
            or DialBeginEvent or DialEndEvent)
        {
            RememberPeerActivity(ExtractEndpointFromChannel(e.Channel), DateTimeOffset.UtcNow);
            schedule = true;
        }

        if (!schedule)
            return;

        _ = _hub.Clients.Group("monitor").SendAsync("amiEvent", new
        {
            type = e.GetType().Name,
            channel = e.Channel,
            uniqueId = e.UniqueId,
            at = DateTimeOffset.UtcNow
        });

        ScheduleSnapshotPush(EventDebounce);
    }

    private async Task RunPollLoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            await PublishSnapshotsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_pollTimer is null)
            return;

        try
        {
            while (await _pollTimer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await PublishSnapshotsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private void ScheduleSnapshotPush(TimeSpan delay)
    {
        CancellationToken token;
        lock (_debounceGate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            token = _debounceCts.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
                await PublishSnapshotsAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // superseded or shutdown
            }
        }, CancellationToken.None);
    }

    private async Task PublishSnapshotsAsync(CancellationToken ct)
    {
        if (!_ami.GetStatus().Connected)
            return;

        if (!await _publishLock.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        try
        {
            IReadOnlyList<PeerDto> peers;
            IReadOnlyList<ChannelDto> channels;
            try
            {
                (peers, channels) = await BuildSnapshotAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Monitor live snapshot skipped (AMI busy/error)");
                return;
            }

            var group = _hub.Clients.Group("monitor");
            await group.SendAsync("peersUpdated", peers, ct).ConfigureAwait(false);
            await group.SendAsync("channelsUpdated", channels, ct).ConfigureAwait(false);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private void RememberPeerActivity(string? peerOrId, DateTimeOffset atUtc)
    {
        var key = NormalizePeerKey(peerOrId);
        if (string.IsNullOrEmpty(key))
            return;
        _peerLastActivity[key] = atUtc;
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        var status = _ami.GetStatus();
        _ = _hub.Clients.Group("monitor").SendAsync("connectionUpdated", status);
        _ = _hub.Clients.Group("monitor").SendAsync("connectionStatus", status);
        if (status.Connected)
            ScheduleSnapshotPush(TimeSpan.FromMilliseconds(200));
    }

    private static bool PeerMatchesChannel(PeerDto peer, ChannelDto channel)
    {
        var peerKey = NormalizePeerKey(peer.Id);
        var endpoint = ExtractEndpointFromChannel(channel.Channel);
        if (!string.IsNullOrEmpty(endpoint) && PeerKeyEquals(peerKey, endpoint))
            return true;

        if (!string.IsNullOrWhiteSpace(channel.Exten)
            && PeerKeyEquals(peerKey, NormalizePeerKey(channel.Exten)))
            return true;

        // Trunk peer "fanava_trunk 3191…" ↔ channel endpoint "fanava_trunk"
        var firstToken = peerKey.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrEmpty(firstToken)
            && !string.IsNullOrEmpty(endpoint)
            && endpoint.StartsWith(firstToken, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool PeerKeyEquals(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
        || string.Equals(a.Replace(" ", "", StringComparison.Ordinal), b.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);

    private static ChannelDto? PickBestChannel(IReadOnlyList<ChannelDto> channels)
    {
        if (channels.Count == 0)
            return null;

        return channels
            .OrderByDescending(c => ChannelStateRank(c.State))
            .ThenByDescending(c => c.DurationSeconds ?? 0)
            .First();
    }

    private static int ChannelStateRank(string? state)
    {
        var s = (state ?? string.Empty).Trim().ToLowerInvariant();
        if (s is "up" or "busy") return 40;
        if (s.Contains("ring")) return 30;
        if (s is "dialing" or "proceeding" or "progress") return 20;
        if (s is "down" or "rsrvd" or "reserved") return 5;
        return 10;
    }

    private static string? DeriveCallState(ChannelDto? channel, string? deviceState)
    {
        if (channel is not null)
        {
            var s = (channel.State ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Contains("ring")) return "Ringing";
            if (s is "up" or "busy") return "InUse";
            if (s is "dialing" or "proceeding" or "progress") return "Dialing";
            if (!string.IsNullOrEmpty(s) && s is not "down")
                return "InUse";
        }

        if (string.IsNullOrWhiteSpace(deviceState))
            return channel is null ? null : "InUse";

        var d = deviceState.Trim().ToUpperInvariant().Replace(" ", "_", StringComparison.Ordinal);
        return d switch
        {
            "NOT_INUSE" or "NOTINUSE" or "INVALID" or "UNKNOWN" => null,
            "INUSE" or "IN_USE" or "BUSY" or "ONHOLD" or "ON_HOLD" => "InUse",
            "RINGING" or "RINGINUSE" or "RING_INUSE" => "Ringing",
            "UNAVAILABLE" => "Unavailable",
            _ => d.Contains("RING", StringComparison.Ordinal) ? "Ringing"
                : d.Contains("USE", StringComparison.Ordinal) || d.Contains("BUSY", StringComparison.Ordinal) ? "InUse"
                : null
        };
    }

    private static bool IsActiveCallState(string? callState) =>
        callState is "Ringing" or "InUse" or "Dialing" or "Busy";

    private static string FormatCallDisplayStatus(string callState, int? durationSeconds)
    {
        if (durationSeconds is > 0)
            return $"{callState} ({durationSeconds}s)";
        return callState;
    }

    private static string? ExtractEndpointFromChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            return null;

        var raw = channel.Trim();
        // Local/100@from-internal-00000001;1
        if (raw.StartsWith("Local/", StringComparison.OrdinalIgnoreCase))
        {
            var body = raw["Local/".Length..];
            var at = body.IndexOf('@');
            var local = at > 0 ? body[..at] : body.Split('-', 2)[0];
            return NormalizePeerKey(local);
        }

        var slash = raw.IndexOf('/');
        var rest = slash >= 0 && slash < raw.Length - 1 ? raw[(slash + 1)..] : raw;
        // Drop ;2 / ;1
        var semi = rest.IndexOf(';');
        if (semi >= 0)
            rest = rest[..semi];

        // Drop -0000001a unique suffix when present
        var dash = rest.LastIndexOf('-');
        if (dash > 0 && dash < rest.Length - 1)
        {
            var suffix = rest[(dash + 1)..];
            if (LooksLikeUniqueSuffix(suffix))
                rest = rest[..dash];
        }

        return NormalizePeerKey(rest);
    }

    private static bool LooksLikeUniqueSuffix(string suffix) =>
        suffix.Length >= 6 && suffix.All(c => char.IsLetterOrDigit(c));

    private static string ResolveDeviceName(DeviceStateChangeEvent e)
    {
        if (e.Attributes is not null)
        {
            if (e.Attributes.TryGetValue("Device", out var device) && !string.IsNullOrWhiteSpace(device))
                return device;
            if (e.Attributes.TryGetValue("device", out var device2) && !string.IsNullOrWhiteSpace(device2))
                return device2;
        }

        return e.Channel ?? string.Empty;
    }

    private static IEnumerable<string> DeviceKeysForPeer(PeerDto peer)
    {
        var key = NormalizePeerKey(peer.Id);
        yield return key;
        if (!string.IsNullOrWhiteSpace(peer.Tech))
            yield return NormalizePeerKey($"{peer.Tech}/{peer.Id}");
    }

    private static string MapExtensionStatusCode(int status) => status switch
    {
        0 => "NOT_INUSE",
        1 => "INUSE",
        2 => "BUSY",
        4 => "UNAVAILABLE",
        8 => "RINGING",
        16 => "ONHOLD",
        _ => status.ToString()
    };

    private static string NormalizePeerKey(string? peerOrId)
    {
        if (string.IsNullOrWhiteSpace(peerOrId))
            return string.Empty;
        var raw = peerOrId.Trim();
        var slash = raw.LastIndexOf('/');
        return slash >= 0 && slash < raw.Length - 1 ? raw[(slash + 1)..] : raw;
    }
}
