using System.Collections.Concurrent;
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

public sealed class CallJobStore : ICallJobStore
{
    private readonly ConcurrentDictionary<string, CallJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public CallJob Add(CallJob job)
    {
        _jobs[job.Id] = job;
        return job;
    }

    public bool TryGet(string id, out CallJob? job) => _jobs.TryGetValue(id, out job);

    public IReadOnlyList<CallJob> GetAll() =>
        _jobs.Values.OrderByDescending(j => j.CreatedAtUtc).ToList();

    public void Update(CallJob job)
    {
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _jobs[job.Id] = job;
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
}
