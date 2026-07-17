using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/Peers")]
public sealed class PeersController : ControllerBase
{
    private readonly IMonitorService _monitor;

    public PeersController(IMonitorService monitor) => _monitor = monitor;

    [HttpGet("GetList")]
    public async Task<ActionResult<ApiResult<PeerDto>>> GetList(CancellationToken cancellationToken)
    {
        try
        {
            var peers = await _monitor.GetPeersAsync(cancellationToken);
            return Ok(ApiResult<PeerDto>.Ok(peers));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<PeerDto>.Fail(ex.Message));
        }
    }
}
