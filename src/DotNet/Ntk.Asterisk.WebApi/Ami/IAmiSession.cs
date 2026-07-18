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
    /// <summary>Probe status for every enabled AMI server (live session reused; others Login/Logoff).</summary>
    Task<IReadOnlyList<ConnectionStatusDto>> GetStatusListAsync(CancellationToken cancellationToken = default);
    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    /// <summary>Disconnect then reconnect using persisted Admin Settings; returns final status.</summary>
    Task<ConnectionStatusDto> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
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
