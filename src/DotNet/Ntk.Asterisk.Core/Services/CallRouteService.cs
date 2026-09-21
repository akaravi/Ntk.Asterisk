using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core.Contracts;
using Ntk.Asterisk.Domain.CallRouting;

namespace Ntk.Asterisk.Core.Services;

public interface ICallRouteService
{
    Task<IReadOnlyList<CallRouteDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CallRouteDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<CallRouteDto> CreateAsync(CreateCallRouteRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<CallRouteDto?> UpdateAsync(string id, UpdateCallRouteRequest request, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, string? performedBy = null, CancellationToken cancellationToken = default);
    Task<FastAgiLookupResponse> MatchRouteAsync(FastAgiLookupRequest request, CancellationToken cancellationToken = default);
}

public class CallRouteService : ICallRouteService
{
    private readonly ICallRouteRepository _repository;
    private readonly IAuditLogger _auditLogger;

    public CallRouteService(ICallRouteRepository repository, IAuditLogger auditLogger)
    {
        _repository = repository;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<CallRouteDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var routes = await _repository.GetAllAsync(cancellationToken);
        return routes.Select(MapToDto).ToList();
    }

    public async Task<CallRouteDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var route = await _repository.GetByIdAsync(id, cancellationToken);
        return route != null ? MapToDto(route) : null;
    }

    public async Task<CallRouteDto> CreateAsync(CreateCallRouteRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var route = new CallRoute(
            id: Guid.NewGuid().ToString("N"),
            pattern: request.Pattern,
            destination: request.Destination,
            description: request.Description,
            priority: request.Priority,
            serverId: request.ServerId
        );

        await _repository.AddAsync(route, cancellationToken);

        await _auditLogger.LogAsync(
            action: "CreateCallRoute",
            category: "CallRouting",
            performedBy: performedBy,
            serverId: request.ServerId,
            targetResource: route.Id,
            details: $"Created route {route.Pattern} -> {route.Destination}",
            cancellationToken: cancellationToken);

        return MapToDto(route);
    }

    public async Task<CallRouteDto?> UpdateAsync(string id, UpdateCallRouteRequest request, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var route = await _repository.GetByIdAsync(id, cancellationToken);
        if (route == null) return null;

        route.Update(request.Pattern, request.Destination, request.Description, request.Priority, request.IsEnabled, request.ServerId);
        await _repository.UpdateAsync(route, cancellationToken);

        await _auditLogger.LogAsync(
            action: "UpdateCallRoute",
            category: "CallRouting",
            performedBy: performedBy,
            serverId: request.ServerId,
            targetResource: route.Id,
            details: $"Updated route {route.Pattern} -> {route.Destination}",
            cancellationToken: cancellationToken);

        return MapToDto(route);
    }

    public async Task<bool> DeleteAsync(string id, string? performedBy = null, CancellationToken cancellationToken = default)
    {
        var route = await _repository.GetByIdAsync(id, cancellationToken);
        if (route == null) return false;

        await _repository.DeleteAsync(id, cancellationToken);

        await _auditLogger.LogAsync(
            action: "DeleteCallRoute",
            category: "CallRouting",
            performedBy: performedBy,
            serverId: route.ServerId,
            targetResource: id,
            details: $"Deleted route {route.Pattern}",
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<FastAgiLookupResponse> MatchRouteAsync(FastAgiLookupRequest request, CancellationToken cancellationToken = default)
    {
        var routes = await _repository.GetAllAsync(cancellationToken);
        var enabledRoutes = routes.Where(r => r.IsEnabled).OrderBy(r => r.Priority).ToList();

        foreach (var route in enabledRoutes)
        {
            if (IsPatternMatch(route.Pattern, request.Did) || IsPatternMatch(route.Pattern, request.CallerId))
            {
                return new FastAgiLookupResponse
                {
                    RouteFound = true,
                    RouteId = route.Id,
                    Destination = route.Destination,
                    Description = route.Description,
                    ChannelVariables = new Dictionary<string, string>
                    {
                        ["MATCHED_ROUTE_ID"] = route.Id,
                        ["MATCHED_ROUTE_PATTERN"] = route.Pattern,
                        ["MATCHED_DESTINATION"] = route.Destination
                    }
                };
            }
        }

        return new FastAgiLookupResponse { RouteFound = false };
    }

    private static bool IsPatternMatch(string pattern, string input)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(input)) return false;
        if (pattern == "_" || pattern == ".*") return true;
        if (pattern.StartsWith("_")) pattern = pattern.Substring(1);

        try
        {
            var regexPattern = "^" + pattern
                .Replace("X", "[0-9]")
                .Replace("Z", "[1-9]")
                .Replace("N", "[2-9]")
                .Replace(".", ".*")
                .Replace("!", ".*") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(input, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return input.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static CallRouteDto MapToDto(CallRoute route) => new()
    {
        Id = route.Id,
        Pattern = route.Pattern,
        Destination = route.Destination,
        Description = route.Description,
        Priority = route.Priority,
        IsEnabled = route.IsEnabled,
        ServerId = route.ServerId
    };
}
