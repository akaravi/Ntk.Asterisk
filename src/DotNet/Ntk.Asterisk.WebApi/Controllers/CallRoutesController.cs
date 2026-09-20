using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/CallRoutes")]
public sealed class CallRoutesController : ControllerBase
{
    private readonly ICallRouteStore _store;
    private readonly ILogger<CallRoutesController> _logger;

    public CallRoutesController(ICallRouteStore store, ILogger<CallRoutesController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpGet("GetList")]
    public ActionResult<ApiResult<CallRouteDto>> GetList(
        [FromQuery] string? quickSearch = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var list = _store.GetList(quickSearch, isActive);
            return Ok(ApiResult<CallRouteDto>.Ok(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting smart call routes list");
            return Ok(ApiResult<CallRouteDto>.Fail(ex.Message));
        }
    }

    [HttpGet("GetById")]
    public ActionResult<ApiResult<CallRouteDto>> GetById([FromQuery] string id)
    {
        try
        {
            var item = _store.GetById(id);
            if (item == null)
            {
                return Ok(ApiResult<CallRouteDto>.Fail("Call route not found."));
            }
            return Ok(ApiResult<CallRouteDto>.Ok(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting smart call route {Id}", id);
            return Ok(ApiResult<CallRouteDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Save")]
    public ActionResult<ApiResult<CallRouteDto>> Save([FromBody] CallRouteUpsertRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.CallerNumber))
            {
                return Ok(ApiResult<CallRouteDto>.Fail("Caller number is required."));
            }

            var saved = _store.Upsert(request);
            return Ok(ApiResult<CallRouteDto>.Ok(saved));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving smart call route");
            return Ok(ApiResult<CallRouteDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Delete")]
    public ActionResult<ApiResult<bool>> Delete([FromQuery] string id)
    {
        try
        {
            var result = _store.Delete(id);
            return Ok(ApiResult<bool>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting smart call route {Id}", id);
            return Ok(ApiResult<bool>.Fail(ex.Message));
        }
    }

    [HttpPost("ToggleActive")]
    public ActionResult<ApiResult<CallRouteDto>> ToggleActive([FromQuery] string id)
    {
        try
        {
            var item = _store.ToggleActive(id);
            if (item == null)
            {
                return Ok(ApiResult<CallRouteDto>.Fail("Call route not found."));
            }
            return Ok(ApiResult<CallRouteDto>.Ok(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling smart call route {Id}", id);
            return Ok(ApiResult<CallRouteDto>.Fail(ex.Message));
        }
    }
    [HttpPost("Reorder")]
    public ActionResult<ApiResult<bool>> Reorder([FromBody] CallRouteReorderRequest request)
    {
        try
        {
            if (request?.Items == null || request.Items.Count == 0)
            {
                return Ok(ApiResult<bool>.Fail("Items list is required."));
            }

            var success = _store.Reorder(request.Items);
            return Ok(ApiResult<bool>.Ok(success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering smart call routes");
            return Ok(ApiResult<bool>.Fail(ex.Message));
        }
    }


    [HttpGet("Lookup")]
    [HttpPost("Lookup")]
    public ActionResult<ApiResult<CallRouteLookupResultDto>> Lookup(
        [FromQuery] string? callerNumber,
        [FromBody] CallRouteLookupRequest? body)
    {
        try
        {
            var number = !string.IsNullOrWhiteSpace(callerNumber) ? callerNumber : body?.CallerNumber;
            if (string.IsNullOrWhiteSpace(number))
            {
                return Ok(ApiResult<CallRouteLookupResultDto>.Fail("Caller number is required for lookup."));
            }

            var req = body ?? new CallRouteLookupRequest();
            req.CallerNumber = number.Trim();
            if (string.IsNullOrWhiteSpace(req.Source))
            {
                req.Source = "manual";
            }

            var result = _store.Lookup(req);
            return Ok(ApiResult<CallRouteLookupResultDto>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing smart call route lookup for {Caller}", callerNumber);
            return Ok(ApiResult<CallRouteLookupResultDto>.Fail(ex.Message));
        }
    }

    [HttpGet("GetRecentDecisions")]
    public ActionResult<ApiResult<SmartRouteDecisionDto>> GetRecentDecisions([FromQuery] int limit = 50)
    {
        try
        {
            var list = _store.GetRecentDecisions(limit);
            return Ok(ApiResult<SmartRouteDecisionDto>.Ok(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent smart route decisions");
            return Ok(ApiResult<SmartRouteDecisionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ReportDecision")]
    public ActionResult<ApiResult<SmartRouteDecisionDto>> ReportDecision([FromBody] SmartRouteReportRequest request)
    {
        try
        {
            if (request == null)
            {
                return Ok(ApiResult<SmartRouteDecisionDto>.Fail("Report request is required."));
            }
            var decision = _store.ReportDecision(request);
            return Ok(ApiResult<SmartRouteDecisionDto>.Ok(decision));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting smart route decision outcome");
            return Ok(ApiResult<SmartRouteDecisionDto>.Fail(ex.Message));
        }
    }
}
