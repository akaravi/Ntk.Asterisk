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
    private readonly IAsteriskSettingsService _settings;
    private readonly ILogger<AmiSession> _logger;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ManagerConnection? _connection;
    private DateTimeOffset? _connectedAtUtc;
    private string? _lastError;
    private int _connecting;

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
            lock (_gate) return _connection;
        }
    }

    public ConnectionStatusDto GetStatus()
    {
        var opt = _settings.GetEffective();
        var active = _settings.GetActiveServer();
        var connected = false;
        string? version = null;
        lock (_gate)
        {
            connected = _connection?.IsConnected() == true;
            version = connected ? _connection?.Version : null;
        }

        double? uptime = null;
        if (connected && _connectedAtUtc.HasValue)
            uptime = (DateTimeOffset.UtcNow - _connectedAtUtc.Value).TotalSeconds;

        return new ConnectionStatusDto
        {
            Connected = connected,
            Configured = opt.IsConfigured,
            Host = string.IsNullOrWhiteSpace(opt.Host) ? null : opt.Host,
            Port = opt.Port,
            LastError = _lastError,
            ConnectedAtUtc = connected ? _connectedAtUtc : null,
            UptimeSeconds = uptime,
            AsteriskVersion = version,
            ChannelTech = opt.ChannelTech,
            DefaultTrunk = opt.DefaultTrunk,
            AmiHostConfigured = !string.IsNullOrWhiteSpace(opt.Host),
            AmiPortConfigured = opt.Port is > 0,
            AmiUserConfigured = !string.IsNullOrWhiteSpace(opt.Username),
            AmiSecretConfigured = !string.IsNullOrWhiteSpace(opt.Secret),
            TrunkPeerFilterConfigured = !string.IsNullOrWhiteSpace(opt.TrunkPeerFilter),
            ServerId = active?.Id,
            ServerName = active?.Name,
            Username = active?.Username,
            IsEnabled = active?.IsEnabled ?? false,
            IsDefault = active?.IsDefault ?? false,
            IsLiveSession = true
        };
    }

    public async Task<IReadOnlyList<ConnectionStatusDto>> GetStatusListAsync(
        CancellationToken cancellationToken = default)
    {
        var enabled = _settings.GetEnabledServerConfigs();
        if (enabled.Count == 0)
            return Array.Empty<ConnectionStatusDto>();

        var live = GetStatus();
        var liveId = live.ServerId;

        var tasks = enabled.Select(server => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(liveId)
                && string.Equals(server.Id, liveId, StringComparison.OrdinalIgnoreCase))
            {
                return FromLive(live, server);
            }

            return ProbeServer(server);
        }, cancellationToken)).ToArray();

        var rows = await Task.WhenAll(tasks).ConfigureAwait(false);
        return rows
            .OrderByDescending(r => r.IsDefault)
            .ThenByDescending(r => r.IsLiveSession)
            .ThenBy(r => r.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ConnectionStatusDto FromLive(ConnectionStatusDto live, AsteriskServerConfig server) => new()
    {
        Connected = live.Connected,
        Configured = live.Configured,
        Host = live.Host,
        Port = live.Port,
        LastError = live.LastError,
        ConnectedAtUtc = live.ConnectedAtUtc,
        UptimeSeconds = live.UptimeSeconds,
        AsteriskVersion = live.AsteriskVersion,
        ChannelTech = server.ChannelTech,
        DefaultTrunk = server.DefaultTrunk,
        AmiHostConfigured = live.AmiHostConfigured,
        AmiPortConfigured = live.AmiPortConfigured,
        AmiUserConfigured = live.AmiUserConfigured,
        AmiSecretConfigured = live.AmiSecretConfigured,
        TrunkPeerFilterConfigured = !string.IsNullOrWhiteSpace(server.TrunkPeerFilter),
        ServerId = server.Id,
        ServerName = server.Name,
        Username = server.Username,
        IsEnabled = server.IsEnabled,
        IsDefault = server.IsDefault,
        IsLiveSession = true
    };

    private ConnectionStatusDto ProbeServer(AsteriskServerConfig server)
    {
        if (!server.IsConfigured)
        {
            return new ConnectionStatusDto
            {
                Connected = false,
                Configured = false,
                Host = string.IsNullOrWhiteSpace(server.Host) ? null : server.Host,
                Port = server.Port,
                LastError = "AMI not configured. Set Host/Port/Username/Secret.",
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
                IsLiveSession = false
            };
        }

        ManagerConnection? conn = null;
        try
        {
            conn = new ManagerConnection(server.Host!, server.Port!.Value, server.Username!, server.Secret!)
            {
                KeepAlive = false,
                PingInterval = 0,
                FireAllEvents = false
            };
            conn.Login();
            return new ConnectionStatusDto
            {
                Connected = true,
                Configured = true,
                Host = server.Host,
                Port = server.Port,
                LastError = null,
                ConnectedAtUtc = DateTimeOffset.UtcNow,
                UptimeSeconds = 0,
                AsteriskVersion = conn.Version,
                ChannelTech = server.ChannelTech,
                DefaultTrunk = server.DefaultTrunk,
                AmiHostConfigured = true,
                AmiPortConfigured = true,
                AmiUserConfigured = true,
                AmiSecretConfigured = true,
                TrunkPeerFilterConfigured = !string.IsNullOrWhiteSpace(server.TrunkPeerFilter),
                ServerId = server.Id,
                ServerName = server.Name,
                Username = server.Username,
                IsEnabled = server.IsEnabled,
                IsDefault = server.IsDefault,
                IsLiveSession = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AMI probe failed for {Name} {Host}:{Port}", server.Name, server.Host, server.Port);
            return new ConnectionStatusDto
            {
                Connected = false,
                Configured = true,
                Host = server.Host,
                Port = server.Port,
                LastError = ex.Message,
                ChannelTech = server.ChannelTech,
                DefaultTrunk = server.DefaultTrunk,
                AmiHostConfigured = true,
                AmiPortConfigured = true,
                AmiUserConfigured = true,
                AmiSecretConfigured = true,
                TrunkPeerFilterConfigured = !string.IsNullOrWhiteSpace(server.TrunkPeerFilter),
                ServerId = server.Id,
                ServerName = server.Name,
                Username = server.Username,
                IsEnabled = server.IsEnabled,
                IsDefault = server.IsDefault,
                IsLiveSession = false
            };
        }
        finally
        {
            if (conn != null)
            {
                try { conn.Logoff(); } catch { /* ignore */ }
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_settings.GetEffective().AutoConnectOnStartup)
            _ = Task.Run(() => EnsureConnectedAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisconnectAsync();
    }

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        var opt = _settings.GetEffective();
        if (!opt.IsConfigured)
        {
            _lastError = "AMI not configured. Set Host/Port/Username/Secret in Admin Settings.";
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        lock (_gate)
        {
            if (_connection?.IsConnected() == true)
                return;
        }

        if (Interlocked.CompareExchange(ref _connecting, 1, 0) != 0)
            return;

        try
        {
            await Task.Run(() => ConnectCore(opt), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _connecting, 0);
        }
    }

    public async Task<ConnectionStatusDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        // Wait briefly if a background connect is still finishing.
        var spins = 0;
        while (Interlocked.CompareExchange(ref _connecting, 0, 0) != 0 && spins < 50)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            spins++;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return GetStatus();
    }

    private void ConnectCore(AsteriskOptions opt)
    {
        lock (_gate)
        {
            if (_connection?.IsConnected() == true)
                return;

            try
            {
                _connection?.Logoff();
            }
            catch
            {
                // ignore dispose of stale connection
            }

            var conn = new ManagerConnection(opt.Host!, opt.Port!.Value, opt.Username!, opt.Secret!)
            {
                KeepAlive = opt.KeepAlive,
                PingInterval = Math.Max(0, opt.PingIntervalMs),
                FireAllEvents = true
            };

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

            try
            {
                conn.Login();
                _connection = conn;
                _connectedAtUtc = DateTimeOffset.UtcNow;
                _lastError = null;
                _logger.LogInformation(
                    "AMI connected to {Host}:{Port} version={Version}",
                    opt.Host, opt.Port, conn.Version);
            }
            catch (Exception ex)
            {
                _connection = null;
                _connectedAtUtc = null;
                _lastError = ex.Message;
                _logger.LogWarning(ex, "AMI login failed for {Host}:{Port}", opt.Host, opt.Port);
                try { conn.Logoff(); } catch { /* ignore */ }
            }
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnConnectionState(object? sender, ConnectionStateEvent e) => RaiseAmi(e);

    private void OnUnhandledEvent(object? sender, ManagerEvent e) => RaiseAmi(e);

    private void RaiseAmi(ManagerEvent e) => AmiEvent?.Invoke(this, e);

    public Task DisconnectAsync()
    {
        lock (_gate)
        {
            try
            {
                _connection?.Logoff();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AMI logoff error");
            }

            _connection = null;
            _connectedAtUtc = null;
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task<ManagerResponse> SendActionAsync(
        ManagerAction action,
        CancellationToken cancellationToken = default,
        int? timeoutMs = null)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var conn = Connection ?? throw new InvalidOperationException(_lastError ?? "AMI not connected.");
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
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var conn = Connection ?? throw new InvalidOperationException(_lastError ?? "AMI not connected.");
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
        try { _connection?.Logoff(); } catch { /* ignore */ }
        _connection = null;
    }
}
