using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Core.Contracts;
using Ntk.Asterisk.Domain.Monitoring;
using Ntk.Asterisk.Monitoring.Security;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Response;

namespace Ntk.Asterisk.Monitoring.Intervention;

public interface IAudioInterventionService
{
    Task<ApiResult> SpyChannelAsync(ChanSpyRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> HangupChannelAsync(HangupRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> BridgeChannelsAsync(BridgeRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
}

public class AudioInterventionService : IAudioInterventionService
{
    private readonly IAmiSession _amiSession;
    private readonly IMonitoringAclService _aclService;
    private readonly IOptionsMonitor<MonitoringOptions> _monitoringOptions;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AudioInterventionService> _logger;

    public AudioInterventionService(
        IAmiSession amiSession,
        IMonitoringAclService aclService,
        IOptionsMonitor<MonitoringOptions> monitoringOptions,
        IAuditLogger auditLogger,
        ILogger<AudioInterventionService> logger)
    {
        _amiSession = amiSession;
        _aclService = aclService;
        _monitoringOptions = monitoringOptions;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<ApiResult> SpyChannelAsync(ChanSpyRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var canSpy = await _aclService.CanSpyChannelAsync(performedBy, request.TargetChannel, cancellationToken);
        if (!canSpy)
        {
            return ApiResult.Fail("Unauthorized: You do not have permission to spy on this channel.");
        }

        try
        {
            var spyFlags = request.Mode.ToLowerInvariant() switch
            {
                "whisper" => "wb",
                "privatewhisper" => "Wb",
                "barge" => "Bb",
                "dtmf" => "db",
                "quiet" => "qb",
                _ => "b"
            };

            var tech = _monitoringOptions.CurrentValue.DefaultChannelTech;
            var channelToOriginate = request.SpyingExtension.Contains('/')
                ? request.SpyingExtension
                : $"{tech}/{request.SpyingExtension}";

            var originate = new OriginateAction
            {
                Channel = channelToOriginate,
                Application = "ChanSpy",
                Data = $"{request.TargetChannel},{spyFlags}",
                CallerId = $"Spy: {request.TargetChannel}",
                Async = true,
                Priority = "1"
            };

            var response = await _amiSession.SendActionAsync(originate, request.ServerId, cancellationToken);
            if (response is ManagerError error)
            {
                _logger.LogWarning("ChanSpy originate failed: {Message}", error.Message);
                return ApiResult.Fail($"Asterisk error: {error.Message}");
            }

            await _auditLogger.LogAsync(
                action: "ChanSpy",
                category: "Monitoring.Intervention",
                performedBy: performedBy,
                serverId: request.ServerId,
                targetResource: request.TargetChannel,
                details: $"Originated ChanSpy on {request.TargetChannel} by {request.SpyingExtension} (Mode: {request.Mode})",
                cancellationToken: cancellationToken);

            return ApiResult.Ok($"ChanSpy initiated on channel {request.TargetChannel}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute ChanSpy on {Channel}", request.TargetChannel);
            return ApiResult.Fail(ex.Message);
        }
    }

    public async Task<ApiResult> HangupChannelAsync(HangupRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var canIntervene = await _aclService.CanInterveneAsync(performedBy, request.Channel, cancellationToken);
        if (!canIntervene)
        {
            return ApiResult.Fail("Unauthorized: You do not have permission to hangup this channel.");
        }

        try
        {
            var hangupAction = new HangupAction(request.Channel);
            var response = await _amiSession.SendActionAsync(hangupAction, request.ServerId, cancellationToken);

            if (response is ManagerError error)
            {
                return ApiResult.Fail($"Failed to hangup channel: {error.Message}");
            }

            await _auditLogger.LogAsync(
                action: "HangupChannel",
                category: "Monitoring.Intervention",
                performedBy: performedBy,
                serverId: request.ServerId,
                targetResource: request.Channel,
                details: $"Hung up channel {request.Channel}",
                cancellationToken: cancellationToken);

            return ApiResult.Ok($"Channel {request.Channel} hung up.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hangup channel {Channel}", request.Channel);
            return ApiResult.Fail(ex.Message);
        }
    }

    public async Task<ApiResult> BridgeChannelsAsync(BridgeRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var canIntervene1 = await _aclService.CanInterveneAsync(performedBy, request.Channel1, cancellationToken);
        var canIntervene2 = await _aclService.CanInterveneAsync(performedBy, request.Channel2, cancellationToken);
        if (!canIntervene1 || !canIntervene2)
        {
            return ApiResult.Fail("Unauthorized: You do not have permission to bridge these channels.");
        }

        try
        {
            var bridgeAction = new BridgeAction
            {
                Channel1 = request.Channel1,
                Channel2 = request.Channel2,
                Tone = request.Tone ? "yes" : "no"
            };

            var response = await _amiSession.SendActionAsync(bridgeAction, request.ServerId, cancellationToken);
            if (response is ManagerError error)
            {
                return ApiResult.Fail($"Failed to bridge channels: {error.Message}");
            }

            await _auditLogger.LogAsync(
                action: "BridgeChannels",
                category: "Monitoring.Intervention",
                performedBy: performedBy,
                serverId: request.ServerId,
                targetResource: $"{request.Channel1} <-> {request.Channel2}",
                details: $"Bridged channels {request.Channel1} and {request.Channel2}",
                cancellationToken: cancellationToken);

            return ApiResult.Ok($"Bridged channels {request.Channel1} and {request.Channel2}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bridge channels {Ch1} and {Ch2}", request.Channel1, request.Channel2);
            return ApiResult.Fail(ex.Message);
        }
    }
}
