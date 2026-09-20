using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/FastAgi")]
public sealed class FastAgiController : ControllerBase
{
    private readonly IFastAgiTelemetryService _telemetry;
    private readonly ILogger<FastAgiController> _logger;
    private static readonly HashSet<int> AllowedPorts = new() { 4573, 4572 };
    private static readonly HashSet<string> AllowedScripts = new(StringComparer.OrdinalIgnoreCase) { "smartroute", "customivr" };

    public FastAgiController(IFastAgiTelemetryService telemetry, ILogger<FastAgiController> logger)
    {
        _telemetry = telemetry;
        _logger = logger;
    }

    [HttpGet("GetStatus")]
    public async Task<ActionResult<ApiResult<FastAgiStatusDto>>> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _telemetry.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            return Ok(ApiResult<FastAgiStatusDto>.Ok(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting FastAGI status");
            return Ok(ApiResult<FastAgiStatusDto>.Fail(ex.Message));
        }
    }

    [HttpGet("GetRecentPackets")]
    public ActionResult<ApiResult<FastAgiPacketDto>> GetRecentPackets([FromQuery] int limit = 50)
    {
        try
        {
            var list = _telemetry.GetRecentPackets(limit);
            return Ok(ApiResult<FastAgiPacketDto>.Ok(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent FastAGI packets");
            return Ok(ApiResult<FastAgiPacketDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ReportPacket")]
    public ActionResult<ApiResult<FastAgiPacketDto>> ReportPacket([FromBody] FastAgiPacketReportRequest request)
    {
        try
        {
            if (request == null)
            {
                return Ok(ApiResult<FastAgiPacketDto>.Fail("Request is required."));
            }

            if (!AllowedPorts.Contains(request.Port))
            {
                return Ok(ApiResult<FastAgiPacketDto>.Fail($"Invalid FastAGI port {request.Port}. Allowed: 4573, 4572."));
            }

            if (string.IsNullOrWhiteSpace(request.ScriptName) || !AllowedScripts.Contains(request.ScriptName.Trim()))
            {
                return Ok(ApiResult<FastAgiPacketDto>.Fail("Invalid or unsupported script name."));
            }

            // Derive actual remote IP from socket context without fabricating
            request.RemoteClientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Sanitize and bound collection sizes and string lengths
            if (request.Headers != null)
            {
                var bounded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in request.Headers.Take(50))
                {
                    var cleanK = k.Length > 64 ? k[..64] : k;
                    var cleanV = (v ?? string.Empty).Length > 256 ? v![..256] : (v ?? string.Empty);
                    bounded[cleanK] = cleanV;
                }
                request.Headers = bounded;
            }

            if (!string.IsNullOrWhiteSpace(request.Command) && request.Command.Length > 256)
            {
                request.Command = request.Command[..256];
            }

            if (!string.IsNullOrWhiteSpace(request.Note) && request.Note.Length > 512)
            {
                request.Note = request.Note[..512];
            }

            var packet = _telemetry.RecordPacket(request);
            return Ok(ApiResult<FastAgiPacketDto>.Ok(packet));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting FastAGI packet");
            return Ok(ApiResult<FastAgiPacketDto>.Fail(ex.Message));
        }
    }
}
