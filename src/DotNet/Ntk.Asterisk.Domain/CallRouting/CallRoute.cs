using Ntk.Asterisk.Domain.Common;

namespace Ntk.Asterisk.Domain.CallRouting;

public class CallRoute : Entity<string>, IAggregateRoot
{
    public string Pattern { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public string? ServerId { get; private set; }

    private CallRoute() { }

    public CallRoute(string id, string pattern, string destination, string description, int priority, string? serverId = null)
    {
        Id = id;
        Pattern = pattern;
        Destination = destination;
        Description = description;
        Priority = priority;
        ServerId = serverId;
        IsEnabled = true;
    }

    public void Update(string pattern, string destination, string description, int priority, bool isEnabled, string? serverId)
    {
        Pattern = pattern;
        Destination = destination;
        Description = description;
        Priority = priority;
        IsEnabled = isEnabled;
        ServerId = serverId;
    }
}

public interface ICallRouteRepository
{
    Task<IReadOnlyList<CallRoute>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CallRoute?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(CallRoute route, CancellationToken cancellationToken = default);
    Task UpdateAsync(CallRoute route, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
