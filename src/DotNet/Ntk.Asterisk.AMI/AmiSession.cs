using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.AsterNet.AMI.Manager;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.AsterNet.AMI.Manager.Response;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Core.Contracts;

namespace Ntk.Asterisk.AMI;

public class LiveEndpoint : IDisposable
{
    public string ServerId { get; set; } = string.Empty;
    public ManagerConnection? Connection { get; set; }
    public bool Connected => Connection?.IsConnected() == true;
    public DateTime? LastConnectedUtc { get; set; }
    public string? LastError { get; set; }
    public bool ReceiveEvents { get; set; }

    public void Dispose()
    {
        try
        {
            if (Connection?.IsConnected() == true)
                Connection.Logoff();
        }
        catch { }
        finally
        {
            Connection = null;
        }
    }
}

public class AmiSession : IAmiSession, IDisposable
{
    private readonly IOptionsMonitor<AsteriskOptions> _optionsMonitor;
    private readonly ILogger<AmiSession> _logger;
    private readonly ConcurrentDictionary<string, LiveEndpoint> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private bool _disposed;

    public event EventHandler<ManagerEvent>? AmiEvent;
    public event EventHandler? ConnectionChanged;

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

    public AmiSession(IOptionsMonitor<AsteriskOptions> optionsMonitor, ILogger<AmiSession> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public ConnectionStatusDto GetStatus()
    {
        var active = _optionsMonitor.CurrentValue.Servers.FirstOrDefault();
        lock (_gate)
        {
            var ep = active is null ? null : GetOrCreateEndpointUnlocked(active.Id, receiveEvents: true);
            return ToStatusDto(active, ep, isOpsPrimary: true);
        }
    }

    public Task<IReadOnlyList<ConnectionStatusDto>> GetStatusListAsync(CancellationToken cancellationToken = default)
    {
        var servers = _optionsMonitor.CurrentValue.Servers;
        if (servers.Count == 0)
            return Task.FromResult<IReadOnlyList<ConnectionStatusDto>>(Array.Empty<ConnectionStatusDto>());

        var opsId = servers.FirstOrDefault()?.Id;
        List<ConnectionStatusDto> rows;
        lock (_gate)
        {
            rows = servers.Select(server =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isOps = !string.IsNullOrEmpty(opsId) && string.Equals(server.Id, opsId, StringComparison.OrdinalIgnoreCase);
                _endpoints.TryGetValue(server.Id, out var ep);
                return ToStatusDto(server, ep, isOpsPrimary: isOps);
            }).ToList();
        }

        return Task.FromResult<IReadOnlyList<ConnectionStatusDto>>(rows);
    }

    public Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        return EnsureConnectedAsync(null, cancellationToken);
    }

    public Task EnsureConnectedAsync(string? serverId, CancellationToken cancellationToken = default)
    {
        var server = _optionsMonitor.CurrentValue.GetServer(serverId);
        if (server == null)
            throw new InvalidOperationException($"Server '{serverId}' not found in configuration.");

        lock (_gate)
        {
            var isOps = string.IsNullOrWhiteSpace(serverId) || string.Equals(serverId, _optionsMonitor.CurrentValue.Servers.FirstOrDefault()?.Id, StringComparison.OrdinalIgnoreCase);
            var ep = GetOrCreateEndpointUnlocked(server.Id, receiveEvents: isOps);
            ConnectEndpointCore(server, ep);
        }

        return Task.CompletedTask;
    }

    public Task EnsureAllEnabledConnectedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var server in _optionsMonitor.CurrentValue.Servers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                EnsureConnectedAsync(server.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not preconnect to AMI server {ServerId}", server.Id);
            }
        }
        return Task.CompletedTask;
    }

    public async Task<ConnectionStatusDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var defaultServer = _optionsMonitor.CurrentValue.Servers.FirstOrDefault();
        if (defaultServer != null)
        {
            await DisconnectAsync(defaultServer.Id);
            try
            {
                await EnsureConnectedAsync(defaultServer.Id, cancellationToken);
            }
            catch { }
        }
        return GetStatus();
    }

    public Task DisconnectAsync()
    {
        var defaultServer = _optionsMonitor.CurrentValue.Servers.FirstOrDefault();
        if (defaultServer != null)
            return DisconnectAsync(defaultServer.Id);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(string? serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            serverId = _optionsMonitor.CurrentValue.Servers.FirstOrDefault()?.Id;

        if (string.IsNullOrWhiteSpace(serverId))
            return Task.CompletedTask;

        lock (_gate)
        {
            if (_endpoints.TryRemove(serverId, out var ep))
            {
                ep.Dispose();
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAllAsync()
    {
        lock (_gate)
        {
            foreach (var kvp in _endpoints)
            {
                kvp.Value.Dispose();
            }
            _endpoints.Clear();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
        return Task.CompletedTask;
    }

    public async Task<ManagerResponse> SendActionAsync(
        ManagerAction action,
        string? serverId = null,
        CancellationToken cancellationToken = default,
        int? timeoutMs = null)
    {
        await EnsureConnectedAsync(serverId, cancellationToken);

        ManagerConnection? conn;
        lock (_gate)
        {
            var ep = ResolveTargetEndpointUnlocked(serverId);
            conn = ep?.Connection;
        }

        if (conn == null || !conn.IsConnected())
            throw new InvalidOperationException($"Not connected to Asterisk AMI server '{serverId}'.");

        return await Task.Run(() =>
        {
            return timeoutMs.HasValue
                ? conn.SendAction(action, timeoutMs.Value)
                : conn.SendAction(action);
        }, cancellationToken);
    }

    public async Task<ResponseEvents> SendEventGeneratingActionAsync(
        ManagerActionEvent action,
        string? serverId = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(serverId, cancellationToken);

        ManagerConnection? conn;
        lock (_gate)
        {
            var ep = ResolveTargetEndpointUnlocked(serverId);
            conn = ep?.Connection;
        }

        if (conn == null || !conn.IsConnected())
            throw new InvalidOperationException($"Not connected to Asterisk AMI server '{serverId}'.");

        return await Task.Run(() =>
        {
            return timeoutMs.HasValue
                ? conn.SendEventGeneratingAction(action, timeoutMs.Value)
                : conn.SendEventGeneratingAction(action);
        }, cancellationToken);
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
            catch { }

            _logger.LogInformation("Connecting to Asterisk AMI at {Host}:{Port} ({ServerId})...", server.Host, server.Ami.Port, server.Id);

            var conn = new ManagerConnection(server.Host, server.Ami.Port, server.Ami.Username, server.Ami.Password)
            {
                KeepAlive = server.Ami.AutoReconnect,
                FireAllEvents = endpoint.ReceiveEvents
            };

            if (endpoint.ReceiveEvents)
            {
                conn.UnhandledEvent += (s, e) => AmiEvent?.Invoke(this, e);
            }

            try
            {
                conn.Login();
                endpoint.Connection = conn;
                endpoint.LastConnectedUtc = DateTime.UtcNow;
                endpoint.LastError = null;
                _logger.LogInformation("Connected successfully to Asterisk AMI at {Host}:{Port} ({ServerId})", server.Host, server.Ami.Port, server.Id);
            }
            catch (Exception ex)
            {
                endpoint.LastError = ex.Message;
                _logger.LogError(ex, "Failed to connect to Asterisk AMI ({ServerId})", server.Id);
                throw;
            }
            finally
            {
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private LiveEndpoint? ResolveOpsEndpointUnlocked()
    {
        var opsId = _optionsMonitor.CurrentValue.Servers.FirstOrDefault()?.Id;
        if (string.IsNullOrEmpty(opsId)) return null;
        return GetOrCreateEndpointUnlocked(opsId, receiveEvents: true);
    }

    private LiveEndpoint? ResolveTargetEndpointUnlocked(string? serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            return ResolveOpsEndpointUnlocked();

        _endpoints.TryGetValue(serverId, out var ep);
        return ep;
    }

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

    private static ConnectionStatusDto ToStatusDto(AsteriskServerConfig? config, LiveEndpoint? endpoint, bool isOpsPrimary)
    {
        var connected = endpoint?.Connected == true;
        return new ConnectionStatusDto
        {
            Connected = connected,
            Host = config?.Host ?? "127.0.0.1",
            Port = config?.Ami.Port ?? 5038,
            ServerId = config?.Id ?? "default",
            ServerTitle = config?.Id ?? "default",
            IsDefault = isOpsPrimary,
            LastConnectedUtc = endpoint?.LastConnectedUtc,
            LastError = endpoint?.LastError,
            Protocol = "AMI"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisconnectAllAsync();
    }
}
