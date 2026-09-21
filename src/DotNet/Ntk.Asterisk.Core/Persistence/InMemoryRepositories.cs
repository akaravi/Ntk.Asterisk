using System.Collections.Concurrent;
using Ntk.Asterisk.Domain.CallJobs;
using Ntk.Asterisk.Domain.CallRouting;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Core.Persistence;

public class InMemoryCallRouteRepository : ICallRouteRepository
{
    private readonly ConcurrentDictionary<string, CallRoute> _routes = new();

    public Task<IReadOnlyList<CallRoute>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CallRoute>>(_routes.Values.OrderBy(r => r.Priority).ToList());
    }

    public Task<CallRoute?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _routes.TryGetValue(id, out var route);
        return Task.FromResult(route);
    }

    public Task AddAsync(CallRoute route, CancellationToken cancellationToken = default)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CallRoute route, CancellationToken cancellationToken = default)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _routes.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public class InMemoryCallJobRepository : ICallJobRepository
{
    private readonly ConcurrentDictionary<string, CallJob> _jobs = new();

    public Task<IReadOnlyList<CallJob>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CallJob>>(_jobs.Values.OrderByDescending(j => j.CreatedAtUtc).ToList());
    }

    public Task<CallJob?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task AddAsync(CallJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CallJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _jobs.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CallJob>> GetPendingJobsAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var pending = _jobs.Values
            .Where(j => j.Status == CallJobStatus.Pending)
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.CreatedAtUtc)
            .Take(maxCount)
            .ToList();

        return Task.FromResult<IReadOnlyList<CallJob>>(pending);
    }
}

public class InMemoryRecordingMetadataRepository : IRecordingMetadataRepository
{
    private readonly ConcurrentDictionary<string, AudioRecording> _recordings = new();

    public Task<AudioRecording?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _recordings.TryGetValue(id, out var rec);
        return Task.FromResult(rec);
    }

    public Task<AudioRecording?> GetByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        var rec = _recordings.Values.FirstOrDefault(r => r.UniqueId.Equals(uniqueId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(rec);
    }

    public Task<IReadOnlyList<AudioRecording>> SearchAsync(
        string? serverId = null,
        string? extension = null,
        string? callerIdNum = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _recordings.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(serverId))
            query = query.Where(r => r.ServerId.Equals(serverId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(extension))
            query = query.Where(r => r.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(callerIdNum))
            query = query.Where(r => r.CallerIdNum.Contains(callerIdNum, StringComparison.OrdinalIgnoreCase));

        if (fromUtc.HasValue)
            query = query.Where(r => r.StartedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(r => r.StartedAtUtc <= toUtc.Value);

        var list = query
            .OrderByDescending(r => r.StartedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<AudioRecording>>(list);
    }

    public Task AddAsync(AudioRecording recording, CancellationToken cancellationToken = default)
    {
        _recordings[recording.Id] = recording;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AudioRecording recording, CancellationToken cancellationToken = default)
    {
        _recordings[recording.Id] = recording;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _recordings.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AudioRecording>> GetExpiredRecordingsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var expired = _recordings.Values
            .Where(r => !r.IsArchived && r.StartedAtUtc < cutoffUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<AudioRecording>>(expired);
    }
}
