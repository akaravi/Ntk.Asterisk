using Ntk.AsterNet.AMI.Manager;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.AsterNet.AMI.Manager.Response;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Ami;

public interface IAmiSession
{
    event EventHandler<ManagerEvent>? AmiEvent;
    event EventHandler? ConnectionChanged;

    ConnectionStatusDto GetStatus();
    /// <summary>Status for every enabled AMI server from live endpoint map (no Login/Logoff probe).</summary>
    Task<IReadOnlyList<ConnectionStatusDto>> GetStatusListAsync(CancellationToken cancellationToken = default);
    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    /// <summary>Connect live session for serverId, or active default when null/blank.</summary>
    Task EnsureConnectedAsync(string? serverId, CancellationToken cancellationToken = default);
    /// <summary>Connect all enabled configured servers (startup / multi-AMI).</summary>
    Task EnsureAllEnabledConnectedAsync(CancellationToken cancellationToken = default);
    /// <summary>Disconnect then reconnect ops (active default); returns final status.</summary>
    Task<ConnectionStatusDto> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    /// <summary>Disconnect live session for serverId, or active default when null/blank.</summary>
    Task DisconnectAsync(string? serverId);
    Task DisconnectAllAsync();
    Task<ManagerResponse> SendActionAsync(
        ManagerAction action,
        CancellationToken cancellationToken = default,
        int? timeoutMs = null);
    Task<ResponseEvents> SendEventGeneratingActionAsync(
        ManagerActionEvent action,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default);
    ManagerConnection? Connection { get; }
}
