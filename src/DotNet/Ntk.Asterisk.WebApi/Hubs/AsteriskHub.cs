using Microsoft.AspNetCore.SignalR;
using Ntk.Asterisk.WebApi.Services;

namespace Ntk.Asterisk.WebApi.Hubs;

public sealed class AsteriskHub : Hub
{
    public const string Path = "/hubs/asterisk";

    private readonly IQueueAclSessionService _sessions;
    private readonly IQueueAclConnectionRegistry _aclConnections;

    public AsteriskHub(IQueueAclSessionService sessions, IQueueAclConnectionRegistry aclConnections)
    {
        _sessions = sessions;
        _aclConnections = aclConnections;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _aclConnections.Unbind(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeMonitor(string? token = null)
    {
        var principal = _sessions.Resolve(token);
        _aclConnections.Bind(Context.ConnectionId, principal);
        await Groups.AddToGroupAsync(Context.ConnectionId, "monitor").ConfigureAwait(false);
    }

    public async Task UnsubscribeMonitor()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "monitor").ConfigureAwait(false);
    }

    /// <param name="token">Optional queue ACL session token for filtered pushes.</param>
    public async Task SubscribeQueues(string? token = null)
    {
        var principal = _sessions.Resolve(token);
        _aclConnections.Bind(Context.ConnectionId, principal);
        await Groups.AddToGroupAsync(Context.ConnectionId, "queues").ConfigureAwait(false);
    }

    public async Task UnsubscribeQueues()
    {
        _aclConnections.Unbind(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "queues").ConfigureAwait(false);
    }

    public Task SubscribeJobs() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "jobs");

    public Task UnsubscribeJobs() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "jobs");

    public Task SubscribeEvents() =>
        Groups.AddToGroupAsync(Context.ConnectionId, AmiLiveEventFeed.HubGroup);

    public Task UnsubscribeEvents() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AmiLiveEventFeed.HubGroup);

    public Task SubscribeWebPhone() =>
        Groups.AddToGroupAsync(Context.ConnectionId, WebPhonePresenceService.HubGroup);

    public Task UnsubscribeWebPhone() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, WebPhonePresenceService.HubGroup);
}
