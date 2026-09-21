using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ntk.Asterisk.Domain.Audit;

namespace Ntk.Asterisk.AuditLog;

public class AuditLogger : IAuditLogger
{
    private readonly IEnumerable<IAuditLogSink> _sinks;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IEnumerable<IAuditLogSink> sinks, ILogger<AuditLogger> logger)
    {
        _sinks = sinks;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string category,
        string? performedBy = null,
        string? serverId = null,
        string? targetResource = null,
        string? details = null,
        string? ipAddress = null,
        bool isSuccess = true,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry(
            id: Guid.NewGuid().ToString("N"),
            action: action,
            category: category,
            performedBy: performedBy,
            serverId: serverId,
            targetResource: targetResource,
            details: details,
            ipAddress: ipAddress,
            isSuccess: isSuccess,
            timestampUtc: DateTime.UtcNow
        );

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.WriteAsync(entry, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write audit log entry to sink {SinkType}", sink.GetType().Name);
            }
        }
    }
}

public class InMemoryAuditLogSink : IAuditLogSink, IAuditLogRepository
{
    private readonly ConcurrentQueue<AuditLogEntry> _entries = new();
    private const int MaxEntries = 1000;

    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
        return Task.CompletedTask;
    }

    public Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        return WriteAsync(entry, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLogEntry>> SearchAsync(
        string? category = null,
        string? action = null,
        string? performedBy = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(e => e.Action.Equals(action, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(performedBy))
            query = query.Where(e => e.PerformedBy != null && e.PerformedBy.Equals(performedBy, StringComparison.OrdinalIgnoreCase));

        if (fromUtc.HasValue)
            query = query.Where(e => e.TimestampUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(e => e.TimestampUtc <= toUtc.Value);

        var result = query
            .OrderByDescending(e => e.TimestampUtc)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(result);
    }
}

public class ConsoleAuditLogSink : IAuditLogSink
{
    private readonly ILogger<ConsoleAuditLogSink> _logger;

    public ConsoleAuditLogSink(ILogger<ConsoleAuditLogSink> logger)
    {
        _logger = logger;
    }

    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] {TimestampUtc:O} | Action: {Action} | Category: {Category} | PerformedBy: {PerformedBy} | Server: {ServerId} | Target: {TargetResource} | Success: {IsSuccess}",
            entry.TimestampUtc, entry.Action, entry.Category, entry.PerformedBy ?? "N/A", entry.ServerId ?? "N/A", entry.TargetResource ?? "N/A", entry.IsSuccess);
        return Task.CompletedTask;
    }
}

public static class AuditLogServiceCollectionExtensions
{
    public static IServiceCollection AddAsteriskAuditLog(this IServiceCollection services)
    {
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<InMemoryAuditLogSink>();
        services.AddSingleton<IAuditLogSink>(sp => sp.GetRequiredService<InMemoryAuditLogSink>());
        services.AddSingleton<IAuditLogRepository>(sp => sp.GetRequiredService<InMemoryAuditLogSink>());
        services.AddSingleton<IAuditLogSink, ConsoleAuditLogSink>();
        return services;
    }
}
