using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/AsteriskServers")]
public sealed class AsteriskServersController : ControllerBase
{
    private readonly IAsteriskSettingsService _settings;
    private readonly IAmiSession _ami;
    private readonly ILogger<AsteriskServersController> _logger;

    public AsteriskServersController(
        IAsteriskSettingsService settings,
        IAmiSession ami,
        ILogger<AsteriskServersController> logger)
    {
        _settings = settings;
        _ami = ami;
        _logger = logger;
    }

    [HttpGet("GetList")]
    public ActionResult<object> GetList(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] string? quickSearch = null)
    {
        try
        {
            var items = _settings.GetServerList().AsEnumerable();

            if (!string.IsNullOrWhiteSpace(quickSearch))
            {
                var q = quickSearch.Trim();
                items = items.Where(s =>
                    (s.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.Host?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.Username?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.Id?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var sort = (sortBy ?? "name").Trim().ToLowerInvariant();
            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            items = sort switch
            {
                "host" => desc ? items.OrderByDescending(s => s.Host) : items.OrderBy(s => s.Host),
                "port" => desc ? items.OrderByDescending(s => s.Port) : items.OrderBy(s => s.Port),
                "isenabled" or "enabled" => desc
                    ? items.OrderByDescending(s => s.IsEnabled)
                    : items.OrderBy(s => s.IsEnabled),
                "isdefault" or "default" => desc
                    ? items.OrderByDescending(s => s.IsDefault)
                    : items.OrderBy(s => s.IsDefault),
                _ => desc ? items.OrderByDescending(s => s.Name) : items.OrderBy(s => s.Name)
            };

            var list = items.ToList();
            var size = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);
            var index = pageIndex < 0 ? 0 : pageIndex;
            var page = list.Skip(index * size).Take(size).ToList();

            return Ok(new
            {
                isSuccess = true,
                data = page,
                errorMessage = (string?)null,
                totalCount = list.Count,
                pageIndex = index,
                pageSize = size
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers GetList failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    [HttpGet("GetOne/{id}")]
    public ActionResult<ApiResult<AsteriskServerDto>> GetOne(string id)
    {
        var dto = _settings.GetServer(id);
        if (dto == null)
            return Ok(ApiResult<AsteriskServerDto>.Fail($"Server '{id}' not found."));
        return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
    }

    [HttpPost("Add")]
    public async Task<ActionResult<ApiResult<AsteriskServerDto>>> Add(
        [FromBody] AsteriskServerAddRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.AddServer(request);
            await MaybeReconnectAsync(request.ReconnectAfterSave ?? true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers Add failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    [HttpPost("Update")]
    public async Task<ActionResult<ApiResult<AsteriskServerDto>>> Update(
        [FromBody] AsteriskServerUpdateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.UpdateServer(request);
            await MaybeReconnectAsync(request.ReconnectAfterSave ?? true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers Update failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionEnable")]
    public async Task<ActionResult<ApiResult<AsteriskServerDto>>> ActionEnable(
        [FromBody] AsteriskServerIdRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.SetServerEnabled(request.Id, enabled: true);
            await MaybeReconnectAsync(request.ReconnectAfterSave ?? true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers ActionEnable failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionDisable")]
    public async Task<ActionResult<ApiResult<AsteriskServerDto>>> ActionDisable(
        [FromBody] AsteriskServerIdRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.SetServerEnabled(request.Id, enabled: false);
            await MaybeReconnectAsync(request.ReconnectAfterSave ?? true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers ActionDisable failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionSetDefault")]
    public async Task<ActionResult<ApiResult<AsteriskServerDto>>> ActionSetDefault(
        [FromBody] AsteriskServerIdRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.SetDefaultServer(request.Id);
            await MaybeReconnectAsync(request.ReconnectAfterSave ?? true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers ActionSetDefault failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionDelete")]
    public async Task<ActionResult<ApiResult<AsteriskServerDto>>> ActionDelete(
        [FromBody] AsteriskServerIdRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = _settings.DeleteServer(request.Id);
            await MaybeReconnectAsync(request.ReconnectAfterSave ?? true, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResult<AsteriskServerDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AsteriskServers ActionDelete failed");
            return Ok(ApiResult<AsteriskServerDto>.Fail(ex.Message));
        }
    }

    private async Task MaybeReconnectAsync(bool reconnect, CancellationToken cancellationToken)
    {
        if (!reconnect)
            return;

        await _ami.DisconnectAsync().ConfigureAwait(false);
        var opt = _settings.GetEffective();
        if (opt.IsConfigured && opt.AutoConnectOnStartup)
            await _ami.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        else if (opt.IsConfigured)
            await _ami.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
    }
}
