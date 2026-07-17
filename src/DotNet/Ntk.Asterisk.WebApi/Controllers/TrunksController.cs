using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/Trunks")]
public sealed class TrunksController : ControllerBase
{
    private readonly IMonitorService _monitor;

    public TrunksController(IMonitorService monitor) => _monitor = monitor;

    [HttpGet("GetList")]
    public async Task<ActionResult<ApiResult<PeerDto>>> GetList(CancellationToken cancellationToken)
    {
        try
        {
            var trunks = await _monitor.GetTrunksAsync(cancellationToken);
            return Ok(ApiResult<PeerDto>.Ok(trunks));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<PeerDto>.Fail(ex.Message));
        }
    }
}
