using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Health")]
public sealed class HealthController : ControllerBase
{
    private readonly IAmiSession _ami;

    public HealthController(IAmiSession ami)
    {
        _ami = ami;
    }

    [HttpGet]
    public ActionResult<ApiResult<object>> Get()
    {
        var status = _ami.GetStatus();
        var payload = new
        {
            status = "Healthy",
            amiConnected = status.Connected,
            utc = DateTimeOffset.UtcNow.ToString("O")
        };
        return Ok(ApiResult<object>.Ok(payload));
    }
}

[ApiController]
[Route("health")]
public sealed class RootHealthController : ControllerBase
{
    private readonly IAmiSession _ami;

    public RootHealthController(IAmiSession ami)
    {
        _ami = ami;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var status = _ami.GetStatus();
        return Ok(new
        {
            status = "Healthy",
            amiConnected = status.Connected,
            utc = DateTimeOffset.UtcNow.ToString("O")
        });
    }
}
