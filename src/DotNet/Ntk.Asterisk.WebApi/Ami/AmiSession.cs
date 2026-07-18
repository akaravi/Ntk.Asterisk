using Ntk.AsterNet.AMI.Manager;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.AsterNet.AMI.Manager.Response;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Ami;

public sealed class AmiSession : IAmiSession, IHostedService, IDisposable
{
    private sealed class LiveEndpoint
    {
        public required string ServerId { get; init; }
        public ManagerConnection? Connection { get; set; }
        public DateTimeOffset? ConnectedAtUtc { get; set; }
        public string? LastError { get; set; }
        public bool ReceiveEvents { get; set; }
        public int Connecting;
    }

    private readonly IAsteriskSettingsService _settings;
    private readonly ILogger<AmiSession> _logger;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Dictionary<string, LiveEndpoint> _endpoints =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ManagerEvent>? AmiEvent;
    public event EventHandler? ConnectionChanged;

    public AmiSession(IAsteriskSettingsService settings, ILogger<AmiSession> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public ManagerConnection? Connection
    {
        get
        {
            lock (_gate)
            {
                var ops = ResolveOpsEndpointUnlocked();
                return ops?.Connection;
            }
        }
    }

    public ConnectionStatusDto GetStatus()
    {
        var active = _settings.GetActiveServer();
        lock (_gate)
        {
            var ep = active is null
                ? null
                : GetOrCreateEndpointUnlocked(active.Id, receiveEvents: true);
            return ToStatusDto(active, ep, isOpsPrimary: true);
        }
    }

    public async Task<IReadOnlyList<ConnectionStatusDto>> GetStatusListAsync(
        CancellationToken cancellationToken = default)
    {
        var enabled = _settings.GetEnabledServerConfigs();
        if (enabled.Count == 0)
            return Array.Empty<ConnectionStatusDto>();

        var opsId = _settings.GetActiveServer()?.Id;
        List<ConnectionStatusDto> rows;
        lock (_gate)
        {
            rows = enabled.Select(server =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isOps = !string.IsNullOrEmpty(opsId)
                    && string.Equals(server.Id, opsId, StringComparison.OrdinalIgnoreCase);
                _endpoints.TryGetValue(server.Id, out var ep);
                return ToStatusDto(server, ep, isOpsPrimary: isOps);
            }).ToList();
        }

        return await Task.FromResult(
            rows
                .OrderByDescending(r => r.IsDefault)
                .ThenByDescending(r => r.IsLiveSession)
                .ThenBy(r => r.ServerName, StringComparer.OrdinalIgnoreCase)
                .ToList()).ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_settings.GetEffective().AutoConnectOnStartup)
            _ = Task.Run(() => EnsureAllEnabledConnectedAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisconnectAllAsync().ConfigureAwait(false);
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default) =>
        EnsureConnectedAsync(null, cancellationToken);

    public async Task EnsureConnectedAsync(string? serverId, CancellationToken cancellationToken = default)
    {
        var server = ResolveServer(serverId);
        if (server is null)
        {
            lock (_gate)
            {
                var ops = ResolveOpsEndpointUnlocked();
                if (ops is not null)
                    ops.LastError = "AMI not configured. Set Host/Port/Username/Secret in Admin Settings.";
            }

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!server.IsConfigured)
        {
            lock (_gate)
            {
                var ep = GetOrCreateEndpointUnlocked(server.Id, receiveEvents: IsOpsServer(server.Id));
                ep.LastError = "AMI not configured. Set Host/Port/Username/Secret in Admin Settings.";
            }

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        LiveEndpoint endpoint;
        lock (_gate)
        {
            endpoint = GetOrCreateEndpointUnlocked(server.Id, receiveEvents: IsOpsServer(server.Id));
            if (endpoint.Connection?.IsConnected() == true)
                return;
        }

        if (Interlocked.CompareExchange(ref endpoint.Connecting, 1, 0) != 0)
            return;

        try
        {
            await Task.Run(() => ConnectEndpointCore(server, endpoint), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref endpoint.Connecting, 0);
        }
    }

    public async Task EnsureAllEnabledConnectedAsync(CancellationToken cancellationToken = default)
    {
        var enabled = _settings.GetEnabledServerConfigs()
            .Where(s => s.IsConfigured)
            .ToList();
        foreach (var server in enabled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureConnectedAsync(server.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ConnectionStatusDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var opsId = _settings.GetActiveServer()?.Id;
        await DisconnectAsync(opsId).ConfigureAwait(false);

        var spins = 0;
        while (spins < 50)
        {
            lock (_gate)
            {
                var ep = opsId is null ? null : GetEndpointUnlocked(opsId);
                if (ep is null || Volatile.Read(ref ep.Connecting) == 0)
                    break;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            spins++;
        }

        await EnsureConnectedAsync(opsId, cancellationToken).ConfigureAwait(false);
        return GetStatus();
    }

    public Task DisconnectAsync() => DisconnectAsync(null);

    public Task DisconnectAsync(string? serverId)
    {
        lock (_gate)
        {
            var targetId = string.IsNullOrWhiteSpace(serverId)
                ? _settings.GetActiveServer()?.Id
                : serverId.Trim();
            if (string.IsNullOrWhiteSpace(targetId))
                return Task.CompletedTask;

            if (!_endpoints.TryGetValue(targetId, out var ep))
                return Task.CompletedTask;

            LogoffEndpointUnlocked(ep);
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task DisconnectAllAsync()
    {
        lock (_gate)
        {
            foreach (var ep in _endpoints.Values)
                LogoffEndpointUnlocked(ep);
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task<ManagerResponse> SendActionAsync(
        ManagerAction action,
        CancellationToken cancellationToken = default,
        int? timeoutMs = null)
    {
        await EnsureConnectedAsync(null, cancellationToken).ConfigureAwait(false);
        var conn = Connection
            ?? throw new InvalidOperationException(GetOpsLastError() ?? "AMI not connected.");
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => timeoutMs is > 0
                    ? conn.SendAction(action, timeoutMs.Value)
                    : conn.SendAction(action),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<ResponseEvents> SendEventGeneratingActionAsync(
        ManagerActionEvent action,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(null, cancellationToken).ConfigureAwait(false);
        var conn = Connection
            ?? throw new InvalidOperationException(GetOpsLastError() ?? "AMI not connected.");
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => timeoutMs is > 0
                    ? conn.SendEventGeneratingAction(action, timeoutMs.Value)
                    : conn.SendEventGeneratingAction(action),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var ep in _endpoints.Values)
                LogoffEndpointUnlocked(ep);
            _endpoints.Clear();
        }
    }

    private AsteriskServerConfig? ResolveServer(string? serverId)
    {
        if (!string.IsNullOrWhiteSpace(serverId))
        {
            return _settings.GetEnabledServerConfigs()
                .FirstOrDefault(s =>
                    string.Equals(s.Id, serverId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return _settings.GetActiveServer();
    }

    private bool IsOpsServer(string serverId)
    {
        var opsId = _settings.GetActiveServer()?.Id;
        return !string.IsNullOrEmpty(opsId)
            && string.Equals(opsId, serverId, StringComparison.OrdinalIgnoreCase);
    }

    private LiveEndpoint? ResolveOpsEndpointUnlocked()
    {
        var opsId = _settings.GetActiveServer()?.Id;
        if (string.IsNullOrEmpty(opsId))
            return null;
        return GetOrCreateEndpointUnlocked(opsId, receiveEvents: true);
    }

    private LiveEndpoint? GetEndpointUnlocked(string serverId) =>
        _endpoints.TryGetValue(serverId, out var ep) ? ep : null;

    private LiveEndpoint GetOrCreateEndpointUnlocked(string serverId, bool receiveEvents)
    {
        if (!_endpoints.TryGetValue(serverId, out var ep))
        {
            ep = new LiveEndpoint { ServerId = serverId, ReceiveEvents = receiveEvents };
            _endpoints[serverId] = ep;
        }
        else if (receiveEvents)
        {
            ep.ReceiveEvents = true;
        }

        return ep;
    }

    private string? GetOpsLastError()
    {
        lock (_gate)
        {
            return ResolveOpsEndpointUnlocked()?.LastError;
        }
    }

    private void ConnectEndpointCore(AsteriskServerConfig server, LiveEndpoint endpoint)
    {
        lock (_gate)
        {
            if (endpoint.Connection?.IsConnected() == true)
                return;

            try
            {
                endpoint.Connection?.Logoff();
            }
            catch
            {
                // ignore dispose of stale connection
            }

            var opt = server.ToOptions();
            var receiveEvents = IsOpsServer(server.Id);
            endpoint.ReceiveEvents = receiveEvents;

            var conn = new ManagerConnection(opt.Host!, opt.Port!.Value, opt.Username!, opt.Secret!)
            {
                KeepAlive = opt.KeepAlive,
                PingInterval = Math.Max(0, opt.PingIntervalMs),
                FireAllEvents = receiveEvents
            };

            if (receiveEvents)
            {
                conn.UnhandledEvent += OnUnhandledEvent;
                conn.ConnectionState += OnConnectionState;
                conn.OriginateResponse += (_, e) => RaiseAmi(e);
                conn.Hangup += (_, e) => RaiseAmi(e);
                conn.NewChannel += (_, e) => RaiseAmi(e);
                conn.NewState += (_, e) => RaiseAmi(e);
                conn.Bridge += (_, e) => RaiseAmi(e);
                conn.PeerStatus += (_, e) => RaiseAmi(e);
                conn.DialBegin += (_, e) => RaiseAmi(e);
                conn.DeviceStateChanged += (_, e) => RaiseAmi(e);
                conn.ExtensionStatus += (_, e) => RaiseAmi(e);
                conn.MessageWaiting += (_, e) => RaiseAmi(e);
            }

            try
            {
                conn.Login();
                endpoint.Connection = conn;
                endpoint.ConnectedAtUtc = DateTimeOffset.UtcNow;
                endpoint.LastError = null;
                _logger.LogInformation(
                    "AMI live connected ServerId={ServerId} Name={Name} {Host}:{Port} version={Version} events={Events}",
                    server.Id, server.Name, opt.Host, opt.Port, conn.Version, receiveEvents);
            }
            catch (Exception ex)
            {
                endpoint.Connection = null;
                endpoint.ConnectedAtUtc = null;
                endpoint.LastError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "AMI login failed ServerId={ServerId} {Host}:{Port}",
                    server.Id, opt.Host, opt.Port);
                try { conn.Logoff(); } catch { /* ignore */ }
            }
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LogoffEndpointUnlocked(LiveEndpoint ep)
    {
        try
        {
            ep.Connection?.Logoff();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AMI logoff error ServerId={ServerId}", ep.ServerId);
        }

        ep.Connection = null;
        ep.ConnectedAtUtc = null;
    }

    private ConnectionStatusDto ToStatusDto(
        AsteriskServerConfig? server,
        LiveEndpoint? ep,
        bool isOpsPrimary)
    {
        if (server is null)
        {
            return new ConnectionStatusDto
            {
                Connected = false,
                Configured = false,
                LastError = "No AMI server configured.",
                IsLiveSession = false
            };
        }

        var connected = ep?.Connection?.IsConnected() == true;
        double? uptime = null;
        if (connected && ep?.ConnectedAtUtc is { } at)
            uptime = (DateTimeOffset.UtcNow - at).TotalSeconds;

        return new ConnectionStatusDto
        {
            Connected = connected,
            Configured = server.IsConfigured,
            Host = string.IsNullOrWhiteSpace(server.Host) ? null : server.Host,
            Port = server.Port,
            LastError = ep?.LastError,
            ConnectedAtUtc = connected ? ep?.ConnectedAtUtc : null,
            UptimeSeconds = uptime,
            AsteriskVersion = connected ? ep?.Connection?.Version : null,
            ChannelTech = server.ChannelTech,
            DefaultTrunk = server.DefaultTrunk,
            AmiHostConfigured = !string.IsNullOrWhiteSpace(server.Host),
            AmiPortConfigured = server.Port is > 0,
            AmiUserConfigured = !string.IsNullOrWhiteSpace(server.Username),
            AmiSecretConfigured = !string.IsNullOrWhiteSpace(server.Secret),
            TrunkPeerFilterConfigured = !string.IsNullOrWhiteSpace(server.TrunkPeerFilter),
            ServerId = server.Id,
            ServerName = server.Name,
            Username = server.Username,
            IsEnabled = server.IsEnabled,
            IsDefault = server.IsDefault,
            // Persistent live when connected; ops primary always marked live-capable for hub/UI.
            IsLiveSession = connected || isOpsPrimary
        };
    }

    private void OnConnectionState(object? sender, ConnectionStateEvent e) => RaiseAmi(e);

    private void OnUnhandledEvent(object? sender, ManagerEvent e) => RaiseAmi(e);

    private void RaiseAmi(ManagerEvent e) => AmiEvent?.Invoke(this, e);
}
