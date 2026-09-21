using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Core.Contracts;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;

namespace Ntk.Asterisk.Monitoring.Live;

public interface IMonitorService
{
    Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(string? serverId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerDto>> GetPeersAsync(string? serverId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerDto>> GetTrunksAsync(string? serverId = null, CancellationToken cancellationToken = default);
    void HandleAmiEvent(ManagerEvent ev);
}

public sealed class MonitorService : IMonitorService, IDisposable
{
    private readonly IAmiSession _amiSession;
    private readonly ILogger<MonitorService> _logger;
    private readonly ConcurrentDictionary<string, ChannelDto> _channels = new();
    private readonly ConcurrentDictionary<string, PeerDto> _peers = new();

    public MonitorService(IAmiSession amiSession, IOptionsMonitor<MonitoringOptions> _, ILogger<MonitorService> logger)
    {
        _amiSession = amiSession;
        _logger = logger;
        _amiSession.AmiEvent += OnAmiEvent;
    }

    private void OnAmiEvent(object? sender, ManagerEvent ev) => HandleAmiEvent(ev);

    public void HandleAmiEvent(ManagerEvent ev)
    {
        try
        {
            if (ev is HangupEvent)
            {
                _channels.TryRemove(ev.Channel, out _);
                return;
            }

            if (ev is NewChannelEvent channel)
            {
                _channels[channel.Channel] = new ChannelDto
                {
                    ServerId = "default",
                    Channel = channel.Channel,
                    ChannelState = channel.ChannelState ?? string.Empty,
                    ChannelStateDesc = channel.ChannelStateDesc ?? string.Empty,
                    CallerIdNum = channel.CallerIdNum ?? string.Empty,
                    CallerIdName = channel.CallerIdName ?? string.Empty
                };
            }

            if (ev is PeerStatusEvent peer)
            {
                _peers[peer.Peer ?? string.Empty] = new PeerDto
                {
                    ServerId = "default",
                    ObjectName = peer.Peer ?? string.Empty,
                    ChannelType = peer.ChannelType ?? string.Empty,
                    Status = peer.PeerStatus ?? string.Empty,
                    Monitored = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update monitoring state from {EventType}", ev.GetType().Name);
        }
    }

    public async Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(string? serverId = null, CancellationToken cancellationToken = default)
    {
        if (_channels.IsEmpty)
        {
            try
            {
                var result = await _amiSession.SendEventGeneratingActionAsync(new StatusAction(), serverId, cancellationToken: cancellationToken);
                foreach (var item in result.Events.OfType<StatusEvent>())
                {
                    _channels[item.Channel] = new ChannelDto
                    {
                        ServerId = serverId ?? "default",
                        Channel = item.Channel,
                        ChannelState = item.State ?? string.Empty,
                        CallerIdNum = item.CallerIdNum ?? string.Empty,
                        CallerIdName = item.CallerIdName ?? string.Empty,
                        Context = item.Context ?? string.Empty,
                        Extension = item.Extension ?? string.Empty,
                        DurationSeconds = item.Seconds
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not refresh channels for {ServerId}", serverId);
            }
        }

        var items = _channels.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(serverId)) items = items.Where(x => x.ServerId.Equals(serverId, StringComparison.OrdinalIgnoreCase));
        return items.ToList();
    }

    public Task<IReadOnlyList<PeerDto>> GetPeersAsync(string? serverId = null, CancellationToken cancellationToken = default)
    {
        var items = _peers.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(serverId)) items = items.Where(x => x.ServerId.Equals(serverId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<PeerDto>>(items.ToList());
    }

    public Task<IReadOnlyList<PeerDto>> GetTrunksAsync(string? serverId = null, CancellationToken cancellationToken = default)
    {
        var items = _peers.Values.Where(x => x.ObjectName.Contains("trunk", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(serverId)) items = items.Where(x => x.ServerId.Equals(serverId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<PeerDto>>(items.ToList());
    }

    public void Dispose() => _amiSession.AmiEvent -= OnAmiEvent;
}
