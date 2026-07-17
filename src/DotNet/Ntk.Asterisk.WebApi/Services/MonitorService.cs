using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public interface IMonitorService
{
    Task<IReadOnlyList<PeerDto>> GetPeersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerDto>> GetTrunksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default);
    ConfigVisibilityDto GetConfigVisibility();
}

public sealed class MonitorService : IMonitorService, IHostedService
{
    private readonly IAmiSession _ami;
    private readonly IAsteriskSettingsService _settings;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<MonitorService> _logger;

    public MonitorService(
        IAmiSession ami,
        IAsteriskSettingsService settings,
        IHubContext<AsteriskHub> hub,
        ILogger<MonitorService> logger)
    {
        _ami = ami;
        _settings = settings;
        _hub = hub;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent += OnAmiEvent;
        _ami.ConnectionChanged += OnConnectionChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ami.AmiEvent -= OnAmiEvent;
        _ami.ConnectionChanged -= OnConnectionChanged;
        return Task.CompletedTask;
    }

    public ConfigVisibilityDto GetConfigVisibility() => _settings.GetSiteSettings();

    public async Task<IReadOnlyList<PeerDto>> GetPeersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _ami.SendEventGeneratingActionAsync(new SIPPeersAction(), null, cancellationToken)
                .ConfigureAwait(false);
            return events.Events
                .OfType<PeerEntryEvent>()
                .Select(MapPeer)
                .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SIPPeers failed");
            throw;
        }
    }

    public async Task<IReadOnlyList<PeerDto>> GetTrunksAsync(CancellationToken cancellationToken = default)
    {
        var peers = await GetPeersAsync(cancellationToken).ConfigureAwait(false);
        var filter = _settings.GetEffective().TrunkPeerFilter;
        Regex? rx = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try { rx = new Regex(filter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid TrunkPeerFilter regex");
            }
        }

        return peers
            .Where(p => rx is null
                ? p.Id.Contains("trunk", StringComparison.OrdinalIgnoreCase)
                : rx.IsMatch(p.Id))
            .Select(p => new PeerDto
            {
                Id = p.Id,
                Tech = p.Tech,
                Status = p.Status,
                Ip = p.Ip,
                Channel = p.Channel,
                IsTrunk = true
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _ami.SendEventGeneratingActionAsync(new StatusAction(), null, cancellationToken)
                .ConfigureAwait(false);
            return events.Events
                .OfType<StatusEvent>()
                .Select(e => new ChannelDto
                {
                    Channel = e.Channel ?? string.Empty,
                    UniqueId = e.UniqueId,
                    State = e.State,
                    CallerId = e.CallerIdNum ?? e.CallerId,
                    Exten = e.Extension,
                    Context = e.Context,
                    Application = null
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Channel))
                .OrderBy(c => c.Channel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StatusAction failed");
            throw;
        }
    }

    private PeerDto MapPeer(PeerEntryEvent e)
    {
        var id = e.ObjectName ?? string.Empty;
        var filter = _settings.GetEffective().TrunkPeerFilter;
        var isTrunk = false;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try { isTrunk = Regex.IsMatch(id, filter, RegexOptions.IgnoreCase); }
            catch { isTrunk = id.Contains("trunk", StringComparison.OrdinalIgnoreCase); }
        }
        else
        {
            isTrunk = id.Contains("trunk", StringComparison.OrdinalIgnoreCase);
        }

        return new PeerDto
        {
            Id = id,
            Tech = e.ChannelType ?? _settings.GetEffective().ChannelTech,
            Status = e.Status ?? string.Empty,
            Ip = e.IpAddress,
            Channel = null,
            IsTrunk = isTrunk
        };
    }

    private void OnAmiEvent(object? sender, ManagerEvent e)
    {
        if (e is not (PeerStatusEvent or NewChannelEvent or HangupEvent or BridgeEvent))
            return;

        _ = _hub.Clients.Group("monitor").SendAsync("amiEvent", new
        {
            type = e.GetType().Name,
            channel = e.Channel,
            uniqueId = e.UniqueId,
            at = DateTimeOffset.UtcNow
        });
    }

    private void OnConnectionChanged(object? sender, EventArgs e) =>
        _ = _hub.Clients.Group("monitor").SendAsync("connectionStatus", _ami.GetStatus());
}
