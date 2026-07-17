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
    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<ManagerResponse> SendActionAsync(ManagerAction action, CancellationToken cancellationToken = default);
    Task<ResponseEvents> SendEventGeneratingActionAsync(
        ManagerActionEvent action,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default);
    ManagerConnection? Connection { get; }
}
