using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

/// <summary>
/// Decouples <see cref="LiveEventLoggerProvider"/> from <see cref="ILiveEventFeed"/> construction
/// to avoid DI re-entrancy while settings/AMI services are still being created.
/// </summary>
public sealed class LiveEventSinkHolder
{
    private volatile ILiveEventFeed? _feed;

    public void Attach(ILiveEventFeed feed) => _feed = feed;

    public void TryPublish(LiveEventDto item) => _feed?.Publish(item);
}
