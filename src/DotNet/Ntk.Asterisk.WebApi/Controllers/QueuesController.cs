using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/Queues")]
public sealed class QueuesController : ControllerBase
{
    private readonly IQueueMonitorService _queues;
    private readonly IQueueAclStore _acl;
    private readonly IQueueAclSessionService _sessions;

    public QueuesController(
        IQueueMonitorService queues,
        IQueueAclStore acl,
        IQueueAclSessionService sessions)
    {
        _queues = queues;
        _acl = acl;
        _sessions = sessions;
    }

    [HttpGet("GetList")]
    public async Task<ActionResult<ApiResult<QueueDto>>> GetList(CancellationToken cancellationToken)
    {
        try
        {
            if (!TryResolvePrincipal(out var principal, out var fail))
                return Ok(ApiResult<QueueDto>.Fail(fail!));

            var list = await _queues.GetQueuesAsync(cancellationToken);
            list = QueueAclFilter.Apply(list, principal, _acl.IsGateActive);
            return Ok(ApiResult<QueueDto>.Ok(list));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<QueueDto>.Fail(ex.Message));
        }
    }

    [HttpGet("GetOne/{name}")]
    public async Task<ActionResult<ApiResult<QueueDto>>> GetOne(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryResolvePrincipal(out var principal, out var fail))
                return Ok(ApiResult<QueueDto>.Fail(fail!));

            var queue = await _queues.GetQueueAsync(name, cancellationToken);
            if (queue is null)
                return Ok(ApiResult<QueueDto>.Fail($"Queue '{name}' not found."));
            if (!QueueAclFilter.CanAccessQueue(queue, principal, _acl.IsGateActive))
                return Ok(ApiResult<QueueDto>.Fail($"Queue '{name}' not found."));
            return Ok(ApiResult<QueueDto>.Ok(queue));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<QueueDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionPauseMember")]
    public async Task<ActionResult<ApiResult<object>>> ActionPauseMember(
        [FromBody] QueueMemberPauseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryResolvePrincipal(out var principal, out var fail))
                return Ok(ApiResult.Fail(fail!));
            if (!await CanActOnQueueAsync(request.Queue, principal, cancellationToken).ConfigureAwait(false))
                return Ok(ApiResult.Fail("Queue access denied."));

            await _queues.PauseMemberAsync(request, paused: true, cancellationToken);
            return Ok(ApiResult.OkEmpty());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    [HttpPost("ActionUnpauseMember")]
    public async Task<ActionResult<ApiResult<object>>> ActionUnpauseMember(
        [FromBody] QueueMemberPauseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryResolvePrincipal(out var principal, out var fail))
                return Ok(ApiResult.Fail(fail!));
            if (!await CanActOnQueueAsync(request.Queue, principal, cancellationToken).ConfigureAwait(false))
                return Ok(ApiResult.Fail("Queue access denied."));

            await _queues.PauseMemberAsync(request, paused: false, cancellationToken);
            return Ok(ApiResult.OkEmpty());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    [HttpPost("ActionHangupEntry")]
    public async Task<ActionResult<ApiResult<object>>> ActionHangupEntry(
        [FromBody] QueueHangupEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryResolvePrincipal(out var principal, out var fail))
                return Ok(ApiResult.Fail(fail!));
            if (_acl.IsGateActive && principal is null)
                return Ok(ApiResult.Fail("Authentication required."));
            // Hangup by channel — gate only requires auth when active (channel may span queues).
            if (_acl.IsGateActive && principal is not null && !principal.IsAdmin && principal.AllowedQueues.Count == 0)
                return Ok(ApiResult.Fail("Queue access denied."));

            await _queues.HangupEntryAsync(request.Channel, cancellationToken);
            return Ok(ApiResult.OkEmpty());
        }
        catch (Exception ex)
        {
            return Ok(ApiResult.Fail(ex.Message));
        }
    }

    private bool TryResolvePrincipal(out QueueAclPrincipal? principal, out string? fail)
    {
        fail = null;
        principal = _sessions.ResolveFromHttp(Request);
        if (_acl.IsGateActive && principal is null)
        {
            fail = "Authentication required.";
            return false;
        }

        return true;
    }

    private async Task<bool> CanActOnQueueAsync(
        string? queueName,
        QueueAclPrincipal? principal,
        CancellationToken cancellationToken)
    {
        if (!_acl.IsGateActive)
            return true;
        if (principal is null)
            return false;
        if (principal.IsAdmin)
            return true;
        if (string.IsNullOrWhiteSpace(queueName))
            return false;
        var all = await _queues.GetQueuesAsync(cancellationToken).ConfigureAwait(false);
        return QueueAclFilter.CanAccessQueueName(queueName, all, principal, gateActive: true);
    }
}
