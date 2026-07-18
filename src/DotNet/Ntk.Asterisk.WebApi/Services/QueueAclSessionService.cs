using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public sealed class QueueAclPrincipal
{
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = "viewer";
    public IReadOnlySet<string> AllowedQueues { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool IsAdmin => string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);
    public DateTimeOffset ExpiresAtUtc { get; init; }
}

public interface IQueueAclSessionService
{
    QueueAclSessionDto Issue(string userId, string username, string role, IEnumerable<string> allowedQueues);
    QueueAclPrincipal? Resolve(string? token);
    void Revoke(string? token);
    QueueAclPrincipal? ResolveFromHttp(HttpRequest request);
}

public sealed class QueueAclSessionService : IQueueAclSessionService
{
    private readonly ConcurrentDictionary<string, QueueAclPrincipal> _sessions = new(StringComparer.Ordinal);
    private readonly IOptionsMonitor<QueueAclOptions> _options;

    public QueueAclSessionService(IOptionsMonitor<QueueAclOptions> options) => _options = options;

    public QueueAclSessionDto Issue(string userId, string username, string role, IEnumerable<string> allowedQueues)
    {
        var ttl = Math.Clamp(_options.CurrentValue.TokenTtlMinutes, 5, 60 * 24 * 30);
        var expires = DateTimeOffset.UtcNow.AddMinutes(ttl);
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        token = token.Replace("+", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);

        var principal = new QueueAclPrincipal
        {
            UserId = userId,
            Username = username,
            Role = role,
            AllowedQueues = new HashSet<string>(allowedQueues ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            ExpiresAtUtc = expires
        };
        _sessions[token] = principal;

        return new QueueAclSessionDto
        {
            Username = principal.Username,
            Role = principal.Role,
            Token = token,
            ExpiresAtUtc = expires,
            AllowedQueues = principal.AllowedQueues.ToArray(),
            IsAdmin = principal.IsAdmin
        };
    }

    public QueueAclPrincipal? Resolve(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        if (!_sessions.TryGetValue(token.Trim(), out var principal))
            return null;
        if (principal.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token.Trim(), out _);
            return null;
        }

        return principal;
    }

    public void Revoke(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;
        _sessions.TryRemove(token.Trim(), out _);
    }

    public QueueAclPrincipal? ResolveFromHttp(HttpRequest request)
    {
        var token = request.Headers[QueueAclOptions.TokenHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)
            && request.Headers.TryGetValue("Authorization", out var auth))
        {
            var v = auth.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(v) && v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = v["Bearer ".Length..].Trim();
        }

        return Resolve(token);
    }
}

public static class QueueAclFilter
{
    public static IReadOnlyList<QueueDto> Apply(IReadOnlyList<QueueDto> source, QueueAclPrincipal? principal, bool gateActive)
    {
        if (!gateActive || principal is null || principal.IsAdmin)
            return source;

        return source
            .Where(q =>
                principal.AllowedQueues.Contains(q.Name)
                || (!string.IsNullOrWhiteSpace(q.RealName) && principal.AllowedQueues.Contains(q.RealName)))
            .ToList();
    }

    public static bool CanAccessQueue(QueueDto queue, QueueAclPrincipal? principal, bool gateActive)
    {
        if (!gateActive)
            return true;
        if (principal is null)
            return false;
        if (principal.IsAdmin)
            return true;
        return principal.AllowedQueues.Contains(queue.Name)
            || (!string.IsNullOrWhiteSpace(queue.RealName) && principal.AllowedQueues.Contains(queue.RealName));
    }

    public static bool CanAccessQueueName(string name, IReadOnlyList<QueueDto> all, QueueAclPrincipal? principal, bool gateActive)
    {
        if (!gateActive)
            return true;
        if (principal is null)
            return false;
        if (principal.IsAdmin)
            return true;
        var q = all.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.RealName, name, StringComparison.OrdinalIgnoreCase));
        if (q is null)
            return principal.AllowedQueues.Contains(name);
        return CanAccessQueue(q, principal, gateActive);
    }
}

/// <summary>Maps SignalR connection → ACL principal for per-client queue push.</summary>
public interface IQueueAclConnectionRegistry
{
    void Bind(string connectionId, QueueAclPrincipal? principal);
    void Unbind(string connectionId);
    IReadOnlyDictionary<string, QueueAclPrincipal?> Snapshot();
}

public sealed class QueueAclConnectionRegistry : IQueueAclConnectionRegistry
{
    private readonly ConcurrentDictionary<string, QueueAclPrincipal?> _map = new(StringComparer.Ordinal);

    public void Bind(string connectionId, QueueAclPrincipal? principal) =>
        _map[connectionId] = principal;

    public void Unbind(string connectionId) =>
        _map.TryRemove(connectionId, out _);

    public IReadOnlyDictionary<string, QueueAclPrincipal?> Snapshot() =>
        new Dictionary<string, QueueAclPrincipal?>(_map, StringComparer.Ordinal);
}
