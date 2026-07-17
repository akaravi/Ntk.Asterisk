using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Jobs;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/CallJobs")]
public sealed class CallJobsController : ControllerBase
{
    private readonly ICallJobEngine _engine;
    private readonly ICallJobStore _store;
    private readonly ILogger<CallJobsController> _logger;

    public CallJobsController(
        ICallJobEngine engine,
        ICallJobStore store,
        ILogger<CallJobsController> logger)
    {
        _engine = engine;
        _store = store;
        _logger = logger;
    }

    [HttpPost("Add")]
    public async Task<ActionResult<ApiResult<CallJobDto>>> Add(
        [FromBody] CallJobAddRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _engine.AddAsync(request, cancellationToken);
            return Ok(ApiResult<CallJobDto>.Ok(job));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CallJobs Add failed");
            return Ok(ApiResult<CallJobDto>.Fail(ex.Message));
        }
    }

    [HttpGet("GetList")]
    public ActionResult<ApiResult<CallJobDto>> GetList(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? quickSearch = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        var items = _store.GetAll().Select(_store.ToDto).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(quickSearch))
        {
            var q = quickSearch.Trim();
            items = items.Where(j =>
                Contains(j.Id, q)
                || Contains(j.Type, q)
                || Contains(j.State, q)
                || Contains(j.From, q)
                || Contains(j.To, q)
                || Contains(j.Mobile1, q)
                || Contains(j.Mobile2, q)
                || Contains(j.Channel, q));
        }

        items = (sortBy?.ToLowerInvariant(), sortDir?.ToLowerInvariant()) switch
        {
            ("state", "asc") => items.OrderBy(j => j.State),
            ("state", _) => items.OrderByDescending(j => j.State),
            ("type", "asc") => items.OrderBy(j => j.Type),
            ("type", _) => items.OrderByDescending(j => j.Type),
            ("updatedatutc", "asc") => items.OrderBy(j => j.UpdatedAtUtc),
            _ => items.OrderByDescending(j => j.CreatedAtUtc)
        };

        if (pageSize <= 0) pageSize = 50;
        if (pageIndex < 0) pageIndex = 0;
        var page = items.Skip(pageIndex * pageSize).Take(pageSize).ToList();
        return Ok(ApiResult<CallJobDto>.Ok(page));
    }

    [HttpGet("GetOne/{id}")]
    public ActionResult<ApiResult<CallJobDto>> GetOne(string id)
    {
        if (!_store.TryGet(id, out var job) || job is null)
            return Ok(ApiResult<CallJobDto>.Fail($"Job '{id}' not found."));
        return Ok(ApiResult<CallJobDto>.Ok(_store.ToDto(job)));
    }

    [HttpPost("ActionCancel/{id}")]
    public async Task<ActionResult<ApiResult<CallJobDto>>> ActionCancel(string id, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _engine.CancelAsync(id, cancellationToken);
            if (job is null)
                return Ok(ApiResult<CallJobDto>.Fail($"Job '{id}' not found."));
            return Ok(ApiResult<CallJobDto>.Ok(job));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CallJobs ActionCancel failed for {Id}", id);
            return Ok(ApiResult<CallJobDto>.Fail(ex.Message));
        }
    }

    private static bool Contains(string? value, string q) =>
        !string.IsNullOrEmpty(value)
        && value.Contains(q, StringComparison.OrdinalIgnoreCase);
}
