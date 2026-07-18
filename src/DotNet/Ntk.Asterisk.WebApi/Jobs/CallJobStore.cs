using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Jobs;

public interface ICallJobStore
{
    CallJob Add(CallJob job);
    bool TryGet(string id, out CallJob? job);
    IReadOnlyList<CallJob> GetAll();
    void Update(CallJob job);
    CallJobDto ToDto(CallJob job);
}

/// <summary>
/// In-memory job index persisted under App_Data/call-jobs.json so lists survive WebApi restart.
/// </summary>
public sealed class CallJobStore : ICallJobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ConcurrentDictionary<string, CallJob> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _persistGate = new();
    private readonly string _path;
    private readonly string _recordingsDir;
    private readonly ILogger<CallJobStore> _logger;

    public CallJobStore(IHostEnvironment env, ILogger<CallJobStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "call-jobs.json");
        _recordingsDir = Path.Combine(dataDir, "recordings");
        LoadFromDisk();
    }

    public CallJob Add(CallJob job)
    {
        _jobs[job.Id] = job;
        Persist();
        return job;
    }

    public bool TryGet(string id, out CallJob? job) => _jobs.TryGetValue(id, out job);

    public IReadOnlyList<CallJob> GetAll() =>
        _jobs.Values.OrderByDescending(j => j.CreatedAtUtc).ToList();

    public void Update(CallJob job)
    {
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _jobs[job.Id] = job;
        Persist();
    }

    public CallJobDto ToDto(CallJob job)
    {
        var terminal = job.State is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled;
        var started = job.StartedAtUtc ?? (terminal ? job.CreatedAtUtc : null);
        var ended = job.EndedAtUtc ?? (terminal ? job.UpdatedAtUtc : null);
        int? duration = job.DurationSeconds;
        if (duration is null && started is not null && ended is not null)
            duration = (int)Math.Max(0, (ended.Value - started.Value).TotalSeconds);

        return new()
        {
            Id = job.Id,
            Type = job.Type.ToString(),
            State = ToApiState(job.State),
            From = job.From,
            To = job.To,
            Mobile1 = job.Mobile1,
            Mobile2 = job.Mobile2,
            ActionId = job.ActionId,
            Channel = job.Channel,
            UniqueId = job.UniqueId,
            ErrorMessage = job.ErrorMessage,
            ResultReason = job.ResultReason ?? job.ErrorMessage,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc,
            StartedAtUtc = started,
            EndedAtUtc = ended,
            DurationSeconds = duration,
            IsCommandJob = job.IsCommandJob,
            HasRecording = job.RecordingStarted || !string.IsNullOrWhiteSpace(job.RecordingFileName),
            RecordingFileName = job.RecordingFileName,
            RecordingAvailable = job.RecordingAvailable
        };
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_path))
        {
            _logger.LogInformation("No persisted call jobs at {Path}", _path);
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<CallJobsDocument>(json, JsonOptions);
            if (doc?.Jobs is null || doc.Jobs.Count == 0)
            {
                _logger.LogInformation("Persisted call-jobs file empty: {Path}", _path);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var orphaned = 0;
            foreach (var job in doc.Jobs)
            {
                if (string.IsNullOrWhiteSpace(job.Id))
                    continue;

                if (!IsTerminal(job.State))
                {
                    job.State = CallJobState.Failed;
                    job.EndedAtUtc ??= now;
                    job.UpdatedAtUtc = now;
                    job.ResultReason ??= "WebApi restarted while job was in progress.";
                    job.ErrorMessage ??= job.ResultReason;
                    orphaned++;
                }

                RefreshRecordingFlag(job);
                _jobs[job.Id] = job;
            }

            _logger.LogInformation(
                "Loaded {Count} call job(s) from {Path} ({Orphaned} marked failed after restart)",
                _jobs.Count,
                _path,
                orphaned);

            if (orphaned > 0)
                Persist();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load call jobs from {Path}", _path);
        }
    }

    private void Persist()
    {
        lock (_persistGate)
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var doc = new CallJobsDocument
                {
                    Version = 1,
                    Jobs = _jobs.Values.OrderByDescending(j => j.CreatedAtUtc).ToList()
                };
                var json = JsonSerializer.Serialize(doc, JsonOptions);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist call jobs to {Path}", _path);
            }
        }
    }

    private void RefreshRecordingFlag(CallJob job)
    {
        if (job.RecordingAvailable)
            return;

        if (string.IsNullOrWhiteSpace(job.RecordingFileName) && !job.RecordingStarted)
            return;

        if (!Directory.Exists(_recordingsDir))
            return;

        // Cache files are ntk-{jobId}.{ext}; friendly name may differ from disk basename.
        foreach (var ext in new[] { ".wav", ".mp3", ".gsm" })
        {
            var byId = Path.Combine(_recordingsDir, $"ntk-{job.Id}{ext}");
            if (File.Exists(byId))
            {
                job.RecordingAvailable = true;
                job.RecordingStarted = true;
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(job.RecordingFileName))
        {
            var byName = Path.Combine(_recordingsDir, job.RecordingFileName);
            if (File.Exists(byName))
            {
                job.RecordingAvailable = true;
                job.RecordingStarted = true;
            }
        }
    }

    private static bool IsTerminal(CallJobState state) =>
        state is CallJobState.Completed or CallJobState.Failed or CallJobState.Cancelled;

    private static string ToApiState(CallJobState state) => state switch
    {
        CallJobState.Queued => "queued",
        CallJobState.DialingLeg1 => "dialing_leg1",
        CallJobState.WaitingAnswer => "waiting_answer",
        CallJobState.DialingLeg2 => "dialing_leg2",
        CallJobState.Bridged => "bridged",
        CallJobState.Completed => "completed",
        CallJobState.Failed => "failed",
        CallJobState.Cancelled => "cancelled",
        _ => state.ToString().ToLowerInvariant()
    };

    private sealed class CallJobsDocument
    {
        public int Version { get; set; } = 1;
        public List<CallJob> Jobs { get; set; } = new();
    }
}
