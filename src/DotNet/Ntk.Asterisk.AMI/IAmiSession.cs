using Ntk.AsterNet.AMI.Manager;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.Manager.Event;
using Ntk.AsterNet.AMI.Manager.Response;
using Ntk.Asterisk.Core.Contracts;

namespace Ntk.Asterisk.AMI;

public interface IAmiSession
{
    event EventHandler<ManagerEvent>? AmiEvent;
    event EventHandler? ConnectionChanged;
    ConnectionStatusDto GetStatus();
    Task<IReadOnlyList<ConnectionStatusDto>> GetStatusListAsync(CancellationToken cancellationToken = default);
    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    Task EnsureConnectedAsync(string? serverId, CancellationToken cancellationToken = default);
    Task EnsureAllEnabledConnectedAsync(CancellationToken cancellationToken = default);
    Task<ConnectionStatusDto> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task DisconnectAsync(string? serverId);
    Task DisconnectAllAsync();
    Task<ManagerResponse> SendActionAsync(ManagerAction action, string? serverId = null, CancellationToken cancellationToken = default, int? timeoutMs = null);
    Task<ResponseEvents> SendEventGeneratingActionAsync(ManagerActionEvent action, string? serverId = null, int? timeoutMs = null, CancellationToken cancellationToken = default);
    ManagerConnection? Connection { get; }
}
