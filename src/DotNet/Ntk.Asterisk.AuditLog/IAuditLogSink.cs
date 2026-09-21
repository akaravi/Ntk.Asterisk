using Ntk.Asterisk.Domain.Audit;

namespace Ntk.Asterisk.AuditLog;

public interface IAuditLogSink
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string category,
        string? performedBy = null,
        string? serverId = null,
        string? targetResource = null,
        string? details = null,
        string? ipAddress = null,
        bool isSuccess = true,
        CancellationToken cancellationToken = default);
}
