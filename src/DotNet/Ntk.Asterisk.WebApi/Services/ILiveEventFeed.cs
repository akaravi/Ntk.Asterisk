using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface ILiveEventFeed
{
    void Publish(LiveEventDto item);
    IReadOnlyList<LiveEventDto> Query(
        string? quickSearch,
        string? source,
        string? level,
        string? sortBy,
        string? sortDir);
    void Clear();
}
