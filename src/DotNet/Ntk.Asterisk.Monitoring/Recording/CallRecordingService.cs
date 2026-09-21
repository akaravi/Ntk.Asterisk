using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Core.Contracts;
using Ntk.Asterisk.Domain.Recording;
using Ntk.Asterisk.Monitoring.Security;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Response;

namespace Ntk.Asterisk.Monitoring.Recording;

public interface ICallRecordingService
{
    Task<ApiResult<AudioRecordingDto>> StartRecordingAsync(RecordChannelRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> StopRecordingAsync(string serverId, string channel, string? mixMonitorId = null, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> PauseRecordingAsync(string serverId, string channel, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> ResumeRecordingAsync(string serverId, string channel, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> MuteRecordingAsync(string serverId, string channel, string direction = "both", string? performedBy = null, CancellationToken cancellationToken = default);
    Task<ApiResult> UnmuteRecordingAsync(string serverId, string channel, string direction = "both", string? performedBy = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioRecordingDto>> SearchRecordingsAsync(string? serverId, string? extension, string? callerIdNum, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancellationToken = default);
    Task<Stream?> GetRecordingAudioStreamAsync(string recordingId, string? performedBy = null, CancellationToken cancellationToken = default);
}

public class CallRecordingService : ICallRecordingService
{
    private readonly IAmiSession _amiSession;
    private readonly IAudioRecordingStorage _storage;
    private readonly IRecordingMetadataRepository _metadataRepository;
    private readonly IMonitoringAclService _aclService;
    private readonly IOptionsMonitor<RecordingOptions> _recordingOptions;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<CallRecordingService> _logger;

    public CallRecordingService(
        IAmiSession amiSession,
        IAudioRecordingStorage storage,
        IRecordingMetadataRepository metadataRepository,
        IMonitoringAclService aclService,
        IOptionsMonitor<RecordingOptions> recordingOptions,
        IAuditLogger auditLogger,
        ILogger<CallRecordingService> logger)
    {
        _amiSession = amiSession;
        _storage = storage;
        _metadataRepository = metadataRepository;
        _aclService = aclService;
        _recordingOptions = recordingOptions;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<ApiResult<AudioRecordingDto>> StartRecordingAsync(RecordChannelRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        // 1. ACL Check
        var canRecord = await _aclService.CanRecordChannelAsync(performedBy, request.Channel, cancellationToken);
        if (!canRecord)
        {
            return ApiResult<AudioRecordingDto>.Fail("Unauthorized: You do not have permission to record this channel.");
        }

        try
        {
            var now = DateTime.UtcNow;
            var format = request.Format.ToString().ToLowerInvariant();
            var callId = Guid.NewGuid().ToString("N");

            var pathFormat = _recordingOptions.CurrentValue.FileSystem.PathFormat;
            var fileName = !string.IsNullOrWhiteSpace(request.CustomFileName)
                ? request.CustomFileName
                : $"rec_{now:yyyyMMdd_HHmmss}_{callId}.{format}";

            var relativePath = FormatStoragePath(pathFormat, now, callId, "unknown", "unknown", "unknown", format, fileName);
            var fullStoragePath = _storage.GetFullPath(relativePath);

            // Execute MixMonitor command via AMI CommandAction with server targeting
            var options = request.MixMonitorOptions ?? "b"; // b = bridge only
            var mixMonitorCmd = $"mixmonitor start {request.Channel} {fullStoragePath} {options}";
            var response = await _amiSession.SendActionAsync(new CommandAction(mixMonitorCmd), request.ServerId, cancellationToken);

            if (response is ManagerError error)
            {
                _logger.LogWarning("Asterisk rejected MixMonitor start: {Message}", error.Message);
                return ApiResult<AudioRecordingDto>.Fail($"Asterisk error: {error.Message}");
            }

            var recording = new AudioRecording(
                id: callId,
                serverId: request.ServerId,
                channel: request.Channel,
                uniqueId: callId,
                callerIdNum: "",
                callerIdName: "",
                connectedLineNum: "",
                extension: "",
                context: "default",
                direction: CallDirection.Unknown,
                relativePath: relativePath,
                fileName: fileName,
                format: request.Format,
                startedAtUtc: now
            );

            await _metadataRepository.AddAsync(recording, cancellationToken);

            await _auditLogger.LogAsync(
                action: "StartRecording",
                category: "Monitoring.Recording",
                performedBy: performedBy,
                serverId: request.ServerId,
                targetResource: request.Channel,
                details: $"MixMonitor started on {request.Channel}, file: {relativePath}",
                cancellationToken: cancellationToken);

            return ApiResult<AudioRecordingDto>.Success(MapToDto(recording));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording on channel {Channel}", request.Channel);
            return ApiResult<AudioRecordingDto>.Fail(ex.Message);
        }
    }

    public async Task<ApiResult> StopRecordingAsync(string serverId, string channel, string? mixMonitorId = null, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _amiSession.SendActionAsync(new StopMixMonitorAction(channel, mixMonitorId), serverId, cancellationToken);
            if (response is ManagerError error)
            {
                return ApiResult.Fail($"Failed to stop recording: {error.Message}");
            }

            await _auditLogger.LogAsync(
                action: "StopRecording",
                category: "Monitoring.Recording",
                performedBy: performedBy,
                serverId: serverId,
                targetResource: channel,
                details: $"StopMixMonitor on {channel}",
                cancellationToken: cancellationToken);

            return ApiResult.Ok("Recording stopped successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording on channel {Channel}", channel);
            return ApiResult.Fail(ex.Message);
        }
    }

    public Task<ApiResult> PauseRecordingAsync(string serverId, string channel, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        // MixMonitor audio muting represents audio recording pause in Asterisk
        return MuteRecordingAsync(serverId, channel, "both", performedBy, cancellationToken);
    }

    public Task<ApiResult> ResumeRecordingAsync(string serverId, string channel, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        return UnmuteRecordingAsync(serverId, channel, "both", performedBy, cancellationToken);
    }

    public async Task<ApiResult> MuteRecordingAsync(string serverId, string channel, string direction = "both", string? performedBy = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _amiSession.SendActionAsync(new MixMonitorMuteAction(channel, direction, 1), serverId, cancellationToken);
            if (response is ManagerError error)
            {
                return ApiResult.Fail($"Failed to mute recording: {error.Message}");
            }

            await _auditLogger.LogAsync(
                action: "MuteRecording",
                category: "Monitoring.Recording",
                performedBy: performedBy,
                serverId: serverId,
                targetResource: channel,
                details: $"MixMonitorMute (1) on {channel}, direction: {direction}",
                cancellationToken: cancellationToken);

            return ApiResult.Ok("Recording muted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mute recording on {Channel}", channel);
            return ApiResult.Fail(ex.Message);
        }
    }

    public async Task<ApiResult> UnmuteRecordingAsync(string serverId, string channel, string direction = "both", string? performedBy = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _amiSession.SendActionAsync(new MixMonitorMuteAction(channel, direction, 0), serverId, cancellationToken);
            if (response is ManagerError error)
            {
                return ApiResult.Fail($"Failed to unmute recording: {error.Message}");
            }

            await _auditLogger.LogAsync(
                action: "UnmuteRecording",
                category: "Monitoring.Recording",
                performedBy: performedBy,
                serverId: serverId,
                targetResource: channel,
                details: $"MixMonitorMute (0) on {channel}, direction: {direction}",
                cancellationToken: cancellationToken);

            return ApiResult.Ok("Recording unmuted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unmute recording on {Channel}", channel);
            return ApiResult.Fail(ex.Message);
        }
    }

    public async Task<IReadOnlyList<AudioRecordingDto>> SearchRecordingsAsync(string? serverId, string? extension, string? callerIdNum, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancellationToken = default)
    {
        var recordings = await _metadataRepository.SearchAsync(serverId, extension, callerIdNum, fromUtc, toUtc, skip, take, cancellationToken);
        return recordings.Select(MapToDto).ToList();
    }

    public async Task<Stream?> GetRecordingAudioStreamAsync(string recordingId, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var rec = await _metadataRepository.GetByIdAsync(recordingId, cancellationToken);
        if (rec == null) return null;

        var canAccess = await _aclService.CanAccessRecordingAsync(performedBy, rec, cancellationToken);
        if (!canAccess)
        {
            _logger.LogWarning("Access denied to recording stream {RecordingId} for user {User}", recordingId, performedBy);
            throw new UnauthorizedAccessException("You are not authorized to access this recording stream.");
        }

        return await _storage.OpenReadAsync(rec.RelativePath, cancellationToken);
    }

    private static string FormatStoragePath(string template, DateTime dt, string callId, string direction, string caller, string called, string ext, string fallbackFileName)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return Path.Combine(dt.ToString("yyyy"), dt.ToString("MM"), dt.ToString("dd"), fallbackFileName);
        }

        var formatted = template
            .Replace("{Year}", dt.ToString("yyyy"))
            .Replace("{Month}", dt.ToString("MM"))
            .Replace("{Day}", dt.ToString("dd"))
            .Replace("{CallId}", callId)
            .Replace("{Direction}", direction)
            .Replace("{Caller}", caller)
            .Replace("{Called}", called)
            .Replace("{Ext}", ext);

        return formatted.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private static AudioRecordingDto MapToDto(AudioRecording r) => new()
    {
        Id = r.Id,
        ServerId = r.ServerId,
        Channel = r.Channel,
        UniqueId = r.UniqueId,
        CallerIdNum = r.CallerIdNum,
        CallerIdName = r.CallerIdName,
        ConnectedLineNum = r.ConnectedLineNum,
        Extension = r.Extension,
        Context = r.Context,
        Direction = r.Direction,
        RelativePath = r.RelativePath,
        FileName = r.FileName,
        Format = r.Format,
        FileSizeBytes = r.FileSizeBytes,
        DurationSeconds = r.DurationSeconds,
        StartedAtUtc = r.StartedAtUtc,
        EndedAtUtc = r.EndedAtUtc,
        IsActive = r.IsActive,
        IsArchived = r.IsArchived
    };
}
