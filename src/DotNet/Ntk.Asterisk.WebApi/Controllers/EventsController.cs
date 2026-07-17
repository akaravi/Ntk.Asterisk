using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Events")]
public sealed class EventsController : ControllerBase
{
    private readonly ILiveEventFeed _feed;
    private readonly ILogger<EventsController> _logger;

    public EventsController(ILiveEventFeed feed, ILogger<EventsController> logger)
    {
        _feed = feed;
        _logger = logger;
    }

    [HttpGet("GetList")]
    public ActionResult<object> GetList(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? quickSearch = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery(Name = "filter.source")] string? filterSource = null,
        [FromQuery(Name = "filter.level")] string? filterLevel = null)
    {
        if (pageSize <= 0) pageSize = 100;
        if (pageSize > 500) pageSize = 500;
        if (pageIndex < 0) pageIndex = 0;

        var materialized = _feed.Query(quickSearch, filterSource, filterLevel, sortBy, sortDir);
        var totalCount = materialized.Count;
        var page = materialized.Skip(pageIndex * pageSize).Take(pageSize).ToList();

        return Ok(new
        {
            isSuccess = true,
            data = page,
            errorMessage = (string?)null,
            totalCount,
            pageIndex,
            pageSize
        });
    }

    [HttpPost("ActionClear")]
    public ActionResult<ApiResult<object>> ActionClear()
    {
        try
        {
            _feed.Clear();
            return Ok(ApiResult<object>.Ok());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Events ActionClear failed");
            return Ok(ApiResult<object>.Fail(ex.Message));
        }
    }
}
