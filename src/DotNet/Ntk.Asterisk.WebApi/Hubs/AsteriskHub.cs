using Microsoft.AspNetCore.SignalR;
using Ntk.Asterisk.WebApi.Services;

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

    public Task SubscribeEvents() =>
        Groups.AddToGroupAsync(Context.ConnectionId, AmiLiveEventFeed.HubGroup);

    public Task UnsubscribeEvents() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AmiLiveEventFeed.HubGroup);
}
