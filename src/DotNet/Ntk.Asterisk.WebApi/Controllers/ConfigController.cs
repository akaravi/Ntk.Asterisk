using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Config")]
public sealed class ConfigController : ControllerBase
{
    private readonly IMonitorService _monitor;

    public ConfigController(IMonitorService monitor) => _monitor = monitor;

    [HttpGet("GetSiteSettings")]
    public ActionResult<ApiResult<ConfigVisibilityDto>> GetSiteSettings() =>
        Ok(ApiResult<ConfigVisibilityDto>.Ok(_monitor.GetConfigVisibility()));
}
