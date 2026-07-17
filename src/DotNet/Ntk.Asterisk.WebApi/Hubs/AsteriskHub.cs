using Microsoft.AspNetCore.SignalR;

namespace Ntk.Asterisk.WebApi.Hubs;

public sealed class AsteriskHub : Hub
{
    public const string Path = "/hubs/asterisk";

    public Task SubscribeMonitor() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "monitor");

    public Task UnsubscribeMonitor() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "monitor");

    public Task SubscribeJobs() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "jobs");

    public Task UnsubscribeJobs() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "jobs");
}
