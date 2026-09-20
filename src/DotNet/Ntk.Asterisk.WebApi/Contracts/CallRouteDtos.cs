namespace Ntk.Asterisk.WebApi.Contracts;

public sealed class CallRouteDto
{
    public string Id { get; set; } = string.Empty;
    public string CallerNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Description { get; set; }
    public string? TargetExtension { get; set; }
    public string? TargetExternalNumber { get; set; }
    public int ExtensionTimeout { get; set; } = 15;
    public int ExternalTimeout { get; set; } = 30;
    public string? OutboundTrunk { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CallRouteUpsertRequest
{
    public string? Id { get; set; }
    public string CallerNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Description { get; set; }
    public string? TargetExtension { get; set; }
    public string? TargetExternalNumber { get; set; }
    public int? ExtensionTimeout { get; set; } = 15;
    public int? ExternalTimeout { get; set; } = 30;
    public string? OutboundTrunk { get; set; }
    public bool? IsActive { get; set; } = true;
    public int? Priority { get; set; } = 1;
}

public sealed class CallRouteOrderItemDto
{
    public string Id { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public sealed class CallRouteReorderRequest
{
    public List<CallRouteOrderItemDto> Items { get; set; } = new();
}

public sealed class CallRouteLookupRequest
{
    public string CallerNumber { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string? Source { get; set; }
    public string? Dnis { get; set; }
}

public sealed class SmartRouteStepDto
{
    public int StepOrder { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? DestinationExtension { get; set; }
    public string? DestinationExternalNumber { get; set; }
    public int ExtensionTimeoutSeconds { get; set; } = 15;
    public int ExternalTimeoutSeconds { get; set; } = 30;
    public string? OutboundTrunk { get; set; }
    public int Priority { get; set; } = 1;
}

public sealed class CallRouteLookupResultDto
{
    public bool Matched { get; set; }
    public CallRouteDto? Route { get; set; }
    public List<SmartRouteStepDto> Steps { get; set; } = new();
    public string Action { get; set; } = "Fallback";
    public string? DestinationExtension { get; set; }
    public string? DestinationExternalNumber { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public int ExtensionTimeoutSeconds { get; set; } = 15;
    public int ExternalTimeoutSeconds { get; set; } = 30;
    public string? OutboundTrunk { get; set; }
    public string NormalizedCallerNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public SmartRouteDecisionDto? Decision { get; set; }
}

public sealed class SmartRouteDecisionDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CallerNumber { get; set; } = string.Empty;
    public string NormalizedCallerNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string Source { get; set; } = "fastagi";
    public bool Matched { get; set; }
    public string Action { get; set; } = "Fallback";
    public string? DestinationExtension { get; set; }
    public string? DestinationExternalNumber { get; set; }
    public string? OutboundTrunk { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public int ExtensionTimeoutSeconds { get; set; } = 15;
    public int ExternalTimeoutSeconds { get; set; } = 30;
    public string Reason { get; set; } = string.Empty;
    public string? MatchedRuleId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Routed";
    public string? Note { get; set; }
}

public sealed class SmartRouteReportRequest
{
    public string? DecisionId { get; set; }
    public string CallerNumber { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string? Action { get; set; }
    public string? Target { get; set; }
    public string Status { get; set; } = "Completed";
    public string? Note { get; set; }
}
