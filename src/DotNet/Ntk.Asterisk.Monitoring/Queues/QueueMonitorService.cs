using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.Core.Contracts;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;

namespace Ntk.Asterisk.Monitoring.Queues;

public interface IQueueMonitorService
{
    Task<IReadOnlyList<QueueSummaryDto>> GetQueueSummariesAsync(string? serverId = null, CancellationToken cancellationToken = default);
}

public class QueueMonitorService : IQueueMonitorService
{
    private readonly IAmiSession _amiSession;
    private readonly ILogger<QueueMonitorService> _logger;
    private readonly ConcurrentDictionary<string, QueueSummaryDto> _queues = new();

    public QueueMonitorService(IAmiSession amiSession, ILogger<QueueMonitorService> logger)
    {
        _amiSession = amiSession;
        _logger = logger;
    }

    public async Task<IReadOnlyList<QueueSummaryDto>> GetQueueSummariesAsync(string? serverId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _amiSession.SendEventGeneratingActionAsync(new QueueStatusAction(), serverId, cancellationToken: cancellationToken);
            var queuesMap = new Dictionary<string, QueueSummaryDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var ev in response.Events)
            {
                if (ev is QueueParamsEvent qpe)
                {
                    if (!queuesMap.TryGetValue(qpe.Queue, out var summary))
                    {
                        summary = new QueueSummaryDto
                        {
                            QueueName = qpe.Queue,
                            CallsWaiting = qpe.Calls,
                            HoldTime = qpe.Holdtime,
                            TalkTime = 0,
                            Completed = qpe.Completed,
                            Abandoned = qpe.Abandoned,
                            ServiceLevel = qpe.ServiceLevel
                        };
                        queuesMap[qpe.Queue] = summary;
                    }
                }
                else if (ev is QueueMemberEvent qme)
                {
                    if (queuesMap.TryGetValue(qme.Queue, out var summary))
                    {
                        summary.Members.Add(new QueueMemberSummaryDto
                        {
                            MemberName = qme.Name ?? qme.Location ?? "",
                            StateInterface = qme.Location ?? "",
                            Status = qme.Status,
                            Paused = qme.Paused,
                            CallsTaken = qme.CallsTaken,
                            LastCall = qme.LastCall
                        });
                    }
                }
            }

            foreach (var kvp in queuesMap)
            {
                _queues[kvp.Key] = kvp.Value;
            }

            return queuesMap.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch queue status via AMI for server {ServerId}", serverId);
            return _queues.Values.ToList();
        }
    }
}
