using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Domain.Recording;
using Ntk.Asterisk.Monitoring.Intervention;
using Ntk.Asterisk.Monitoring.Live;
using Ntk.Asterisk.Monitoring.Queues;
using Ntk.Asterisk.Monitoring.Recording;
using Ntk.Asterisk.Monitoring.Security;
using Ntk.Asterisk.Monitoring.Storage;

namespace Ntk.Asterisk.Monitoring;

public interface IRecordingRetentionService
{
    Task<int> ExecuteRetentionCleanupAsync(CancellationToken cancellationToken = default);
}

public class RecordingRetentionService : IRecordingRetentionService
{
    private readonly IRecordingMetadataRepository _metadataRepository;
    private readonly IAudioRecordingStorage _storage;
    private readonly IOptionsMonitor<RecordingOptions> _recordingOptions;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RecordingRetentionService> _logger;

    public RecordingRetentionService(
        IRecordingMetadataRepository metadataRepository,
        IAudioRecordingStorage storage,
        IOptionsMonitor<RecordingOptions> recordingOptions,
        IAuditLogger auditLogger,
        ILogger<RecordingRetentionService> logger)
    {
        _metadataRepository = metadataRepository;
        _storage = storage;
        _recordingOptions = recordingOptions;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<int> ExecuteRetentionCleanupAsync(CancellationToken cancellationToken = default)
    {
        var retention = _recordingOptions.CurrentValue.Retention;
        if (!retention.Enabled)
        {
            _logger.LogInformation("Recording retention policy is disabled.");
            return 0;
        }

        var cutoffUtc = DateTime.UtcNow.AddDays(-retention.RetentionDays);
        _logger.LogInformation("Executing retention cleanup for recordings prior to {CutoffUtc:O} (Retention: {Days} days)", cutoffUtc, retention.RetentionDays);

        var expiredRecordings = await _metadataRepository.GetExpiredRecordingsAsync(cutoffUtc, cancellationToken);
        var cleanedCount = 0;

        foreach (var rec in expiredRecordings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (retention.AutoArchive)
                {
                    rec.MarkArchived();
                    await _metadataRepository.UpdateAsync(rec, cancellationToken);
                }
                else
                {
                    await _storage.DeleteAsync(rec.RelativePath, cancellationToken);
                    await _metadataRepository.DeleteAsync(rec.Id, cancellationToken);
                }
                cleanedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up expired recording {Id} ({Path})", rec.Id, rec.RelativePath);
            }
        }

        await _auditLogger.LogAsync(
            action: "ExecuteRetentionCleanup",
            category: "Monitoring.Retention",
            details: $"Processed {cleanedCount} expired recordings. (Cutoff: {cutoffUtc:O})",
            cancellationToken: cancellationToken);

        return cleanedCount;
    }
}

public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddAsteriskMonitoring(this IServiceCollection services)
    {
        services.TryAddSingleton<IAudioRecordingStorage, FileSystemAudioRecordingStorage>();
        services.TryAddScoped<IMonitoringAclService, MonitoringAclService>();
        services.TryAddScoped<ICallRecordingService, CallRecordingService>();
        services.TryAddScoped<IAudioInterventionService, AudioInterventionService>();
        services.TryAddSingleton<IMonitorService, MonitorService>();
        services.TryAddSingleton<IQueueMonitorService, QueueMonitorService>();
        services.TryAddScoped<IRecordingRetentionService, RecordingRetentionService>();

        return services;
    }
}
