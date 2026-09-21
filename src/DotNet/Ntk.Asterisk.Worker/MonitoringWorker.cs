using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Monitoring;
using Ntk.Asterisk.Monitoring.Live;

namespace Ntk.Asterisk.Worker;

public sealed class MonitoringWorker(
    IAmiSession amiSession,
    IMonitorService monitorService,
    IRecordingRetentionService retentionService,
    IOptionsMonitor<MonitoringOptions> monitoringOptions,
    IOptionsMonitor<RecordingOptions> recordingOptions,
    ILogger<MonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await amiSession.EnsureAllEnabledConnectedAsync(stoppingToken); }
        catch (Exception ex) { logger.LogWarning(ex, "AMI startup connection failed; worker will continue."); }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(250, monitoringOptions.CurrentValue.SnapshotIntervalMs)));
        var lastCleanup = DateTime.UtcNow.AddHours(-recordingOptions.CurrentValue.Retention.CleanupIntervalHours);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await monitorService.GetChannelsAsync(cancellationToken: stoppingToken);
                await monitorService.GetPeersAsync(cancellationToken: stoppingToken);
                if (recordingOptions.CurrentValue.Retention.Enabled && DateTime.UtcNow - lastCleanup >= TimeSpan.FromHours(recordingOptions.CurrentValue.Retention.CleanupIntervalHours))
                {
                    await retentionService.ExecuteRetentionCleanupAsync(stoppingToken);
                    lastCleanup = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Monitoring worker iteration failed."); }
        }
    }
}
