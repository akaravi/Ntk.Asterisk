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

    /// <summary>Status for every enabled AMI server (live sockets; no probe Login).</summary>
    [HttpGet("GetList")]
    public async Task<ActionResult<object>> GetList(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] string? quickSearch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = (await _ami.GetStatusListAsync(cancellationToken).ConfigureAwait(false))
                .AsEnumerable();

            if (!string.IsNullOrWhiteSpace(quickSearch))
            {
                var q = quickSearch.Trim();
                items = items.Where(s =>
                    (s.ServerName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.Host?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.Username?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.ServerId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (s.LastError?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var sort = (sortBy ?? "name").Trim().ToLowerInvariant();
            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            items = sort switch
            {
                "host" => desc ? items.OrderByDescending(s => s.Host) : items.OrderBy(s => s.Host),
                "connected" => desc
                    ? items.OrderByDescending(s => s.Connected)
                    : items.OrderBy(s => s.Connected),
                "isdefault" or "default" => desc
                    ? items.OrderByDescending(s => s.IsDefault)
                    : items.OrderBy(s => s.IsDefault),
                _ => desc
                    ? items.OrderByDescending(s => s.ServerName)
                    : items.OrderBy(s => s.ServerName)
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
            _logger.LogWarning(ex, "Connection GetList failed");
            return Ok(ApiResult<ConnectionStatusDto>.Fail(ex.Message));
        }
    }

    /// <summary>Connect live AMI. Optional body.serverId (null → active default).</summary>
    [HttpPost("ActionConnect")]
    public async Task<ActionResult<ApiResult<ConnectionStatusDto>>> ActionConnect(
        [FromBody] ConnectionServerRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var serverId = request?.ServerId;
            await _ami.EnsureConnectedAsync(serverId, cancellationToken).ConfigureAwait(false);
            var status = await ResolveStatusAfterActionAsync(serverId, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Connect live AMI for every enabled configured server.</summary>
    [HttpPost("ActionConnectAll")]
    public async Task<ActionResult<ApiResult<ConnectionStatusDto>>> ActionConnectAll(
        CancellationToken cancellationToken)
    {
        try
        {
            await _ami.EnsureAllEnabledConnectedAsync(cancellationToken).ConfigureAwait(false);
            return Ok(ApiResult<ConnectionStatusDto>.Ok(_ami.GetStatus()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AMI ActionConnectAll failed");
            return Ok(ApiResult<ConnectionStatusDto>.Fail(ex.Message));
        }
    }

    /// <summary>Disconnect live AMI. Optional body.serverId (null → active default).</summary>
    [HttpPost("ActionDisconnect")]
    public async Task<ActionResult<ApiResult<ConnectionStatusDto>>> ActionDisconnect(
        [FromBody] ConnectionServerRequest? request,
        CancellationToken cancellationToken)
    {
        var serverId = request?.ServerId;
        await _ami.DisconnectAsync(serverId).ConfigureAwait(false);
        var status = await ResolveStatusAfterActionAsync(serverId, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResult<ConnectionStatusDto>.Ok(status));
    }

    private async Task<ConnectionStatusDto> ResolveStatusAfterActionAsync(
        string? serverId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            return _ami.GetStatus();

        var list = await _ami.GetStatusListAsync(cancellationToken).ConfigureAwait(false);
        return list.FirstOrDefault(s =>
                   string.Equals(s.ServerId, serverId.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? _ami.GetStatus();
    }
}
