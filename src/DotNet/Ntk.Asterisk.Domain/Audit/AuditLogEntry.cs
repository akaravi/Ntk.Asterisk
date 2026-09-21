using Ntk.Asterisk.Domain.Common;

namespace Ntk.Asterisk.Domain.Audit;

public class AuditLogEntry : Entity<string>, IAggregateRoot
{
    public string Action { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string? PerformedBy { get; private set; }
    public string? ServerId { get; private set; }
    public string? TargetResource { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public bool IsSuccess { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(
        string id,
        string action,
        string category,
        string? performedBy,
        string? serverId,
        string? targetResource,
        string? details,
        string? ipAddress,
        bool isSuccess,
        DateTime timestampUtc)
    {
        Id = id;
        Action = action;
        Category = category;
        PerformedBy = performedBy;
        ServerId = serverId;
        TargetResource = targetResource;
        Details = details;
        IpAddress = ipAddress;
        IsSuccess = isSuccess;
        TimestampUtc = timestampUtc;
    }
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> SearchAsync(
        string? category = null,
        string? action = null,
        string? performedBy = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
}
