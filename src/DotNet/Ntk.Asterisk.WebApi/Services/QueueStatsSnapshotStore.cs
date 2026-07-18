using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IQueueStatsSnapshotStore
{
    void Append(IReadOnlyList<QueueDto> queues);
    IReadOnlyList<QueueStatsSampleDto> GetList(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? queue);
}

internal sealed class QueueStatsDocument
{
    public int Version { get; set; } = 1;
    public List<QueueStatsSampleDto> Samples { get; set; } = new();
}

public sealed class QueueStatsSnapshotStore : IQueueStatsSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly IOptionsMonitor<QueueStatsOptions> _options;
    private readonly ILogger<QueueStatsSnapshotStore> _logger;
    private List<QueueStatsSampleDto> _samples;

    public QueueStatsSnapshotStore(
        IHostEnvironment env,
        IOptionsMonitor<QueueStatsOptions> options,
        ILogger<QueueStatsSnapshotStore> logger)
    {
        _options = options;
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "queue-stats-snapshots.json");
        _samples = Load();
    }

    public void Append(IReadOnlyList<QueueDto> queues)
    {
        if (queues is null || queues.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            foreach (var q in queues)
            {
                var members = q.Members ?? Array.Empty<QueueMemberDto>();
                _samples.Add(new QueueStatsSampleDto
                {
                    SnapshotUtc = now,
                    RealName = string.IsNullOrWhiteSpace(q.RealName) ? q.Name : q.RealName,
                    Name = q.Name,
                    CallsWaiting = q.CallsWaiting,
                    Completed = q.Completed,
                    Abandoned = q.Abandoned,
                    AbandonedPercent = q.AbandonedPercent,
                    HoldtimeSeconds = q.HoldtimeSeconds,
                    TalkTimeSeconds = q.TalkTimeSeconds,
                    ServiceLevelPerf = q.ServiceLevelPerf,
                    MemberCount = members.Count,
                    PausedMemberCount = members.Count(m => m.Paused),
                    InCallMemberCount = members.Count(m =>
                        m.StatusLabel.Contains("call", StringComparison.OrdinalIgnoreCase)
                        || m.Status == 2)
                });
            }

            TrimUnlocked();
            PersistUnlocked();
        }
    }

    public IReadOnlyList<QueueStatsSampleDto> GetList(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? queue)
    {
        lock (_gate)
        {
            IEnumerable<QueueStatsSampleDto> q = _samples;
            if (fromUtc is not null)
                q = q.Where(s => s.SnapshotUtc >= fromUtc);
            if (toUtc is not null)
                q = q.Where(s => s.SnapshotUtc <= toUtc);
            if (!string.IsNullOrWhiteSpace(queue))
            {
                var name = queue.Trim();
                q = q.Where(s =>
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.RealName, name, StringComparison.OrdinalIgnoreCase));
            }

            return q.OrderBy(s => s.SnapshotUtc).ToList();
        }
    }

    private void TrimUnlocked()
    {
        var opt = _options.CurrentValue;
        var retention = TimeSpan.FromDays(Math.Clamp(opt.RetentionDays, 1, 365));
        var cutoff = DateTimeOffset.UtcNow - retention;
        _samples = _samples.Where(s => s.SnapshotUtc >= cutoff).ToList();
        var max = Math.Clamp(opt.MaxSamples, 100, 500_000);
        if (_samples.Count > max)
            _samples = _samples.Skip(_samples.Count - max).ToList();
    }

    private List<QueueStatsSampleDto> Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new List<QueueStatsSampleDto>();
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<QueueStatsDocument>(json, JsonOptions);
            return doc?.Samples ?? new List<QueueStatsSampleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load queue stats snapshots");
            return new List<QueueStatsSampleDto>();
        }
    }

    private void PersistUnlocked()
    {
        try
        {
            var doc = new QueueStatsDocument { Samples = _samples };
            File.WriteAllText(_path, JsonSerializer.Serialize(doc, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist queue stats snapshots");
        }
    }
}

/// <summary>Periodically samples live queues into the snapshot store.</summary>
public sealed class QueueStatsSamplerService : IHostedService, IDisposable
{
    private readonly IQueueMonitorService _queues;
    private readonly IQueueStatsSnapshotStore _store;
    private readonly IOptionsMonitor<QueueStatsOptions> _options;
    private readonly ILogger<QueueStatsSamplerService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public QueueStatsSamplerService(
        IQueueMonitorService queues,
        IQueueStatsSnapshotStore store,
        IOptionsMonitor<QueueStatsOptions> options,
        ILogger<QueueStatsSamplerService> logger)
    {
        _queues = queues;
        _store = store;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose() => _cts?.Dispose();

    private async Task RunAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var list = await _queues.GetQueuesAsync(ct).ConfigureAwait(false);
                _store.Append(list);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Queue stats sample skipped");
            }

            var minutes = Math.Clamp(_options.CurrentValue.SnapshotIntervalMinutes, 1, 60);
            try { await Task.Delay(TimeSpan.FromMinutes(minutes), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
}
