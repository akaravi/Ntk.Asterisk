using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/QueueStats")]
public sealed class QueueStatsController : ControllerBase
{
    private readonly IQueueStatsSnapshotStore _store;
    private readonly IQueueAclStore _acl;
    private readonly IQueueAclSessionService _sessions;

    public QueueStatsController(
        IQueueStatsSnapshotStore store,
        IQueueAclStore acl,
        IQueueAclSessionService sessions)
    {
        _store = store;
        _acl = acl;
        _sessions = sessions;
    }

    [HttpGet("GetList")]
    public ActionResult<ApiResult<QueueStatsSampleDto>> GetList(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? queue = null)
    {
        try
        {
            var gate = _acl.IsGateActive;
            var principal = _sessions.ResolveFromHttp(Request);
            if (gate && principal is null)
                return Ok(ApiResult<QueueStatsSampleDto>.Fail("Authentication required."));

            var list = _store.GetList(from, to, queue);
            if (gate && principal is not null && !principal.IsAdmin)
            {
                list = list
                    .Where(s =>
                        principal.AllowedQueues.Contains(s.Name)
                        || principal.AllowedQueues.Contains(s.RealName))
                    .ToList();
            }

            return Ok(ApiResult<QueueStatsSampleDto>.Ok(list));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<QueueStatsSampleDto>.Fail(ex.Message));
        }
    }
}
