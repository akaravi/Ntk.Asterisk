using System.Collections.Concurrent;
using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core.Contracts;
using Ntk.Asterisk.Domain.CallJobs;

namespace Ntk.Asterisk.Core.Services;

public interface ICallJobService
{
    Task<IReadOnlyList<CallJobDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CallJobDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<CallJobDto> CreateAsync(CreateCallJobRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(string id, string? performedBy = null, CancellationToken cancellationToken = default);
}

public class CallJobService : ICallJobService
{
    private readonly ICallJobRepository _repository;
    private readonly IAuditLogger _auditLogger;

    public CallJobService(ICallJobRepository repository, IAuditLogger auditLogger)
    {
        _repository = repository;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<CallJobDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _repository.GetAllAsync(cancellationToken);
        return jobs.Select(MapToDto).ToList();
    }

    public async Task<CallJobDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(id, cancellationToken);
        return job != null ? MapToDto(job) : null;
    }

    public async Task<CallJobDto> CreateAsync(CreateCallJobRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var job = new CallJob(
            id: Guid.NewGuid().ToString("N"),
            serverId: request.ServerId,
            channel: request.Channel,
            context: request.Context,
            extension: request.Extension,
            priority: request.Priority,
            callerId: request.CallerId,
            timeoutMs: request.TimeoutMs,
            variables: request.Variables
        );

        await _repository.AddAsync(job, cancellationToken);

        await _auditLogger.LogAsync(
            action: "CreateCallJob",
            category: "CallJobs",
            performedBy: performedBy,
            serverId: request.ServerId,
            targetResource: job.Id,
            details: $"Enqueued call job to {request.Channel} ({request.Context},{request.Extension},{request.Priority})",
            cancellationToken: cancellationToken);

        return MapToDto(job);
    }

    public async Task<bool> CancelAsync(string id, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(id, cancellationToken);
        if (job == null) return false;

        job.MarkCancelled();
        await _repository.UpdateAsync(job, cancellationToken);

        await _auditLogger.LogAsync(
            action: "CancelCallJob",
            category: "CallJobs",
            performedBy: performedBy,
            serverId: job.ServerId,
            targetResource: id,
            details: $"Cancelled call job {id}",
            cancellationToken: cancellationToken);

        return true;
    }

    private static CallJobDto MapToDto(CallJob job) => new()
    {
        Id = job.Id,
        ServerId = job.ServerId,
        Channel = job.Channel,
        Context = job.Context,
        Extension = job.Extension,
        Priority = job.Priority,
        CallerId = job.CallerId,
        TimeoutMs = job.TimeoutMs,
        Status = job.Status.ToString(),
        CreatedAtUtc = job.CreatedAtUtc,
        StartedAtUtc = job.StartedAtUtc,
        CompletedAtUtc = job.CompletedAtUtc,
        ErrorMessage = job.ErrorMessage,
        Variables = job.Variables
    };
}

public interface IFastAgiTelemetryService
{
    void RecordSession(string scriptName, double durationMs, bool success, string? errorMessage = null);
    IReadOnlyDictionary<string, object> GetStats();
}

public class FastAgiTelemetryService : IFastAgiTelemetryService
{
    private long _totalSessions;
    private long _failedSessions;
    private readonly ConcurrentDictionary<string, long> _scriptUsage = new();

    public void RecordSession(string scriptName, double durationMs, bool success, string? errorMessage = null)
    {
        Interlocked.Increment(ref _totalSessions);
        if (!success) Interlocked.Increment(ref _failedSessions);
        _scriptUsage.AddOrUpdate(scriptName, 1, (_, count) => count + 1);
    }

    public IReadOnlyDictionary<string, object> GetStats()
    {
        return new Dictionary<string, object>
        {
            ["TotalSessions"] = Interlocked.Read(ref _totalSessions),
            ["FailedSessions"] = Interlocked.Read(ref _failedSessions),
            ["SuccessSessions"] = Interlocked.Read(ref _totalSessions) - Interlocked.Read(ref _failedSessions),
            ["ScriptUsage"] = new Dictionary<string, long>(_scriptUsage)
        };
    }
}
