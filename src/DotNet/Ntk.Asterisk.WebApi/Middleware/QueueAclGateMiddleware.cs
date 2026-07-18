using System.Text.Json;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Middleware;

/// <summary>
/// When Queue ACL RequireLogin is active (and users exist), require a valid session token
/// for Admin Panel API routes. WebPhone keeps its own API-key gate.
/// </summary>
public sealed class QueueAclGateMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<QueueAclGateMiddleware> _logger;

    public QueueAclGateMiddleware(RequestDelegate next, ILogger<QueueAclGateMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IQueueAclStore acl,
        IQueueAclSessionService sessions)
    {
        if (!acl.IsGateActive || !IsProtectedApi(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var principal = sessions.ResolveFromHttp(context.Request);
        if (principal is null)
        {
            _logger.LogDebug("Queue ACL gate denied unauthenticated {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteFailAsync(context, "Authentication required.").ConfigureAwait(false);
            return;
        }

        if (RequiresAdmin(context.Request.Path, context.Request.Method) && !principal.IsAdmin)
        {
            _logger.LogDebug("Queue ACL gate denied non-admin {User} {Method} {Path}",
                principal.Username, context.Request.Method, context.Request.Path);
            await WriteFailAsync(context, "Admin role required.").ConfigureAwait(false);
            return;
        }

        context.Items["QueueAclPrincipal"] = principal;
        await _next(context).ConfigureAwait(false);
    }

    private static bool IsProtectedApi(PathString path)
    {
        var p = path.Value ?? string.Empty;
        if (!p.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            return false;

        // Open: Auth, Health, WebPhone (own gate).
        if (p.StartsWith("/api/v1/Auth", StringComparison.OrdinalIgnoreCase))
            return false;
        if (p.StartsWith("/api/v1/Health", StringComparison.OrdinalIgnoreCase))
            return false;
        if (p.StartsWith("/api/v1/WebPhone", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool RequiresAdmin(PathString path, string method)
    {
        var p = path.Value ?? string.Empty;
        if (p.StartsWith("/api/v1/Asterisk/QueueAclUsers", StringComparison.OrdinalIgnoreCase))
            return true;
        if (p.StartsWith("/api/v1/AsteriskServers", StringComparison.OrdinalIgnoreCase))
            return true;
        if (p.StartsWith("/api/v1/Asterisk/CallFiles", StringComparison.OrdinalIgnoreCase))
            return true;
        if (p.StartsWith("/api/v1/Config", StringComparison.OrdinalIgnoreCase)
            && !HttpMethods.IsGet(method))
            return true;
        return false;
    }

    private static async Task WriteFailAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = ApiResult<object>.Fail(message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOpts)).ConfigureAwait(false);
    }
}
