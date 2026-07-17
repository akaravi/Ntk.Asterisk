using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/Connection")]
public sealed class ConnectionController : ControllerBase
{
    private readonly IAmiSession _ami;
    private readonly ILogger<ConnectionController> _logger;

    public ConnectionController(IAmiSession ami, ILogger<ConnectionController> logger)
    {
        _ami = ami;
        _logger = logger;
    }

    [HttpGet("GetStatus")]
    public ActionResult<ApiResult<ConnectionStatusDto>> GetStatus() =>
        Ok(ApiResult<ConnectionStatusDto>.Ok(_ami.GetStatus()));

    [HttpPost("ActionConnect")]
    public async Task<ActionResult<ApiResult<ConnectionStatusDto>>> ActionConnect(CancellationToken cancellationToken)
    {
        try
        {
            await _ami.EnsureConnectedAsync(cancellationToken);
            var status = _ami.GetStatus();
            if (!status.Connected)
                return Ok(ApiResult<ConnectionStatusDto>.Fail(status.LastError ?? "AMI not connected."));
            return Ok(ApiResult<ConnectionStatusDto>.Ok(status));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AMI ActionConnect failed");
            return Ok(ApiResult<ConnectionStatusDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionDisconnect")]
    public async Task<ActionResult<ApiResult<ConnectionStatusDto>>> ActionDisconnect()
    {
        await _ami.DisconnectAsync();
        return Ok(ApiResult<ConnectionStatusDto>.Ok(_ami.GetStatus()));
    }
}
