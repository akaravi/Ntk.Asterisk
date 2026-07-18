using Microsoft.AspNetCore.Mvc;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Jobs;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Controllers;

[ApiController]
[Route("api/v1/Asterisk/Channels")]
public sealed class ChannelsController : ControllerBase
{
    private readonly IMonitorService _monitor;
    private readonly ICallJobEngine _jobs;

    public ChannelsController(IMonitorService monitor, ICallJobEngine jobs)
    {
        _monitor = monitor;
        _jobs = jobs;
    }

    [HttpGet("GetList")]
    public async Task<ActionResult<ApiResult<ChannelDto>>> GetList(CancellationToken cancellationToken)
    {
        try
        {
            var channels = await _monitor.GetChannelsAsync(cancellationToken);
            return Ok(ApiResult<ChannelDto>.Ok(channels));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<ChannelDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionHangup")]
    public async Task<ActionResult<ApiResult<CallJobDto>>> ActionHangup(
        [FromBody] HangupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobs.EnqueueHangupAsync(request.Channel, cancellationToken);
            return Ok(ApiResult<CallJobDto>.Ok(job));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<CallJobDto>.Fail(ex.Message));
        }
    }

    [HttpPost("ActionBridge")]
    public async Task<ActionResult<ApiResult<CallJobDto>>> ActionBridge(
        [FromBody] BridgeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobs.EnqueueBridgeAsync(
                request.Channel1, request.Channel2, request.Tone, cancellationToken);
            return Ok(ApiResult<CallJobDto>.Ok(job));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<CallJobDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Originate supervisor to ExtenSpy (listen/whisper/barge — ChanSpyPro flag matrix).
    /// </summary>
    [HttpPost("ActionChanSpy")]
    public async Task<ActionResult<ApiResult<CallJobDto>>> ActionChanSpy(
        [FromBody] ChanSpyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobs.EnqueueChanSpyAsync(request, cancellationToken);
            return Ok(ApiResult<CallJobDto>.Ok(job));
        }
        catch (Exception ex)
        {
            return Ok(ApiResult<CallJobDto>.Fail(ex.Message));
        }
    }
}
