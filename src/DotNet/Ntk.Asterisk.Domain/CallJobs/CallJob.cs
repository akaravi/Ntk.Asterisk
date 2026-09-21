using Ntk.Asterisk.Domain.Common;

namespace Ntk.Asterisk.Domain.CallJobs;

public enum CallJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public class CallJob : Entity<string>, IAggregateRoot
{
    public string ServerId { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string Context { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public int Priority { get; private set; } = 1;
    public string CallerId { get; private set; } = string.Empty;
    public int TimeoutMs { get; private set; } = 30000;
    public Dictionary<string, string> Variables { get; private set; } = new();
    public CallJobStatus Status { get; private set; } = CallJobStatus.Pending;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }

    private CallJob() { }

    public CallJob(
        string id,
        string serverId,
        string channel,
        string context,
        string extension,
        int priority = 1,
        string callerId = "",
        int timeoutMs = 30000,
        Dictionary<string, string>? variables = null)
    {
        Id = id;
        ServerId = serverId;
        Channel = channel;
        Context = context;
        Extension = extension;
        Priority = priority;
        CallerId = callerId;
        TimeoutMs = timeoutMs;
        Variables = variables ?? new Dictionary<string, string>();
        Status = CallJobStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkStarted()
    {
        Status = CallJobStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = CallJobStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = CallJobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        Status = CallJobStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }
}

public interface ICallJobRepository
{
    Task<IReadOnlyList<CallJob>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CallJob?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(CallJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(CallJob job, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CallJob>> GetPendingJobsAsync(int maxCount, CancellationToken cancellationToken = default);
}
