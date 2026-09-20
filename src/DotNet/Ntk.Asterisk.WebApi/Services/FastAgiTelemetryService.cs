using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public interface IFastAgiTelemetryService
{
    Task<FastAgiStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<FastAgiPacketDto> GetRecentPackets(int limit = 50);
    FastAgiPacketDto RecordPacket(FastAgiPacketReportRequest request);
}

public sealed class FastAgiTelemetryService : IFastAgiTelemetryService
{
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<FastAgiTelemetryService> _logger;
    private readonly object _gate = new();
    private readonly List<FastAgiPacketDto> _packets = new();
    private const int MaxPacketsCapacity = 50;
    private static readonly int[] MonitoredPorts = { 4573, 4572 };

    public FastAgiTelemetryService(IHubContext<AsteriskHub> hub, ILogger<FastAgiTelemetryService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task<FastAgiStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var portStatuses = new List<FastAgiPortStatusDto>();
        var anyListening = false;

        foreach (var port in MonitoredPorts)
        {
            var isListening = await ProbePortListeningAsync(port, cancellationToken).ConfigureAwait(false);
            if (isListening) anyListening = true;

            portStatuses.Add(new FastAgiPortStatusDto
            {
                Port = port,
                IsListening = isListening,
                Status = isListening ? "Listening" : "Offline",
                ErrorMessage = isListening ? null : "Port is not accepting TCP connections.",
                LastCheckedAtUtc = DateTimeOffset.UtcNow
            });
        }

        lock (_gate)
        {
            var lastPacket = _packets.FirstOrDefault();
            return new FastAgiStatusDto
            {
                Ports = portStatuses,
                TotalPacketsReceived = _packets.Count,
                LastPacketAtUtc = lastPacket?.TimestampUtc,
                LastClientIp = lastPacket?.RemoteClientIp,
                OverallStatus = anyListening ? "Active" : "Offline"
            };
        }
    }

    public IReadOnlyList<FastAgiPacketDto> GetRecentPackets(int limit = 50)
    {
        var bounded = Math.Clamp(limit, 1, 100);
        lock (_gate)
        {
            return _packets
                .OrderByDescending(p => p.TimestampUtc)
                .Take(bounded)
                .ToList();
        }
    }

    public FastAgiPacketDto RecordPacket(FastAgiPacketReportRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        FastAgiPacketDto packet;
        var isUpdate = false;

        lock (_gate)
        {
            FastAgiPacketDto? existing = null;
            if (!string.IsNullOrWhiteSpace(request.PacketId))
            {
                existing = _packets.FirstOrDefault(p => string.Equals(p.Id, request.PacketId.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (existing == null && !string.IsNullOrWhiteSpace(request.UniqueId))
            {
                existing = _packets.FirstOrDefault(p => string.Equals(p.UniqueId, request.UniqueId.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (existing != null)
            {
                isUpdate = true;
                if (!string.IsNullOrWhiteSpace(request.Status)) existing.Status = request.Status.Trim();
                if (!string.IsNullOrWhiteSpace(request.Note)) existing.Note = request.Note.Trim();
                if (!string.IsNullOrWhiteSpace(request.Command)) existing.Commands.Add(request.Command.Trim());
                packet = existing;
            }
            else
            {
                packet = new FastAgiPacketDto
                {
                    Id = !string.IsNullOrWhiteSpace(request.PacketId) ? request.PacketId.Trim() : Guid.NewGuid().ToString("N"),
                    Port = request.Port > 0 ? request.Port : 4573,
                    ScriptName = !string.IsNullOrWhiteSpace(request.ScriptName) ? request.ScriptName.Trim() : "smartroute",
                    CallerId = request.CallerId?.Trim() ?? string.Empty,
                    CallerIdName = request.CallerIdName?.Trim(),
                    Channel = request.Channel?.Trim(),
                    UniqueId = request.UniqueId?.Trim(),
                    Context = request.Context?.Trim(),
                    Extension = request.Extension?.Trim(),
                    Priority = request.Priority?.Trim(),
                    RemoteClientIp = request.RemoteClientIp?.Trim(),
                    Status = !string.IsNullOrWhiteSpace(request.Status) ? request.Status.Trim() : "Processing",
                    Note = request.Note?.Trim()
                };

                if (request.Headers != null)
                {
                    foreach (var (k, v) in request.Headers)
                    {
                        packet.Headers[k] = v;
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.Command))
                {
                    packet.Commands.Add(request.Command.Trim());
                }

                _packets.Insert(0, packet);
                while (_packets.Count > MaxPacketsCapacity)
                {
                    _packets.RemoveAt(_packets.Count - 1);
                }
            }
        }

        try
        {
            _ = _hub.Clients.Group("monitor").SendAsync("fastAgiPacket", packet);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to broadcast fastAgiPacket SignalR event");
        }

        return packet;
    }

    private static async Task<bool> ProbePortListeningAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await client.ConnectAsync("127.0.0.1", port, linkedCts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
