using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Config")]
public sealed class ConfigController : ControllerBase
{
    private readonly IAsteriskSettingsService _settings;
    private readonly IAmiSession _ami;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IAsteriskSettingsService settings,
        IAmiSession ami,
        ILogger<ConfigController> logger)
    {
        _settings = settings;
        _ami = ami;
        _logger = logger;
    }

    [HttpGet("GetSiteSettings")]
    public ActionResult<ApiResult<ConfigVisibilityDto>> GetSiteSettings() =>
        Ok(ApiResult<ConfigVisibilityDto>.Ok(_settings.GetSiteSettings()));

    [HttpPost("UpdateSiteSettings")]
    public async Task<ActionResult<ApiResult<ConfigVisibilityDto>>> UpdateSiteSettings(
        [FromBody] AsteriskSiteSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.UpdateSiteSettings(request);
            var reconnect = request.ReconnectAfterSave ?? true;
            if (reconnect)
            {
                await _ami.DisconnectAsync().ConfigureAwait(false);
                if (dto.AmiConfigured)
                    await _ami.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            }

            return Ok(ApiResult<ConfigVisibilityDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateSiteSettings failed");
            return Ok(ApiResult<ConfigVisibilityDto>.Fail(ex.Message));
        }
    }

    /// <summary>Force reconnect with persisted AMI settings and return connection status.</summary>
    [HttpPost("ActionTestConnection")]
    public async Task<ActionResult<ApiResult<ConnectionStatusDto>>> ActionTestConnection(
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _ami.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!status.Configured)
                return Ok(ApiResult<ConnectionStatusDto>.Fail(
                    status.LastError ?? "AMI not configured. Save Host/Port/Username/Secret first."));
            if (!status.Connected)
                return Ok(ApiResult<ConnectionStatusDto>.Fail(
                    status.LastError ?? "AMI connection test failed."));
            return Ok(ApiResult<ConnectionStatusDto>.Ok(status));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ActionTestConnection failed");
            return Ok(ApiResult<ConnectionStatusDto>.Fail(ex.Message));
        }
    }
}
