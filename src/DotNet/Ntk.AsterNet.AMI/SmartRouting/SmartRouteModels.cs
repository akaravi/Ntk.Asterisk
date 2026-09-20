using System;
using System.Collections.Generic;

namespace Ntk.AsterNet.AMI.SmartRouting
{
    /// <summary>
    /// Core routing rule definition shared across all smart routing scenarios.
    /// </summary>
    public class SmartRouteRuleItem
    {
        public string Id { get; set; } = string.Empty;
        public string CallerNumber { get; set; } = string.Empty;
        public string NormalizedCallerNumber { get; set; } = string.Empty;
        public string ContactName { get; set; }
        public string Description { get; set; }
        public string TargetExtension { get; set; }
        public string TargetExternalNumber { get; set; }
        public int ExtensionTimeout { get; set; } = 15;
        public int ExternalTimeout { get; set; } = 30;
        public string OutboundTrunk { get; set; }
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; } = 1;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Individual routing step in a multi-rule cascading sequence.
    /// </summary>
    public class SmartRouteStepItem
    {
        public int StepOrder { get; set; }
        public string RuleId { get; set; } = string.Empty;
        public string ContactName { get; set; }
        public string DestinationExtension { get; set; }
        public string DestinationExternalNumber { get; set; }
        public int ExtensionTimeoutSeconds { get; set; } = 15;
        public int ExternalTimeoutSeconds { get; set; } = 30;
        public string OutboundTrunk { get; set; }
        public int Priority { get; set; } = 1;
    }

    /// <summary>
    /// Result of evaluating a caller against smart routing rules.
    /// Contains primary matched rule as well as the full ordered cascade of matching steps.
    /// </summary>
    public class SmartRouteEvaluationResult
    {
        public bool Matched { get; set; }
        public SmartRouteRuleItem MatchedRule { get; set; }
        public List<SmartRouteStepItem> Steps { get; set; } = new List<SmartRouteStepItem>();
        public string Action { get; set; } = "Fallback"; // "DialExtensionThenForward" | "DialExtensionOnly" | "DialExternalOnly" | "Fallback"
        public string DestinationExtension { get; set; }
        public string DestinationExternalNumber { get; set; }
        public int ExtensionTimeoutSeconds { get; set; } = 15;
        public int ExternalTimeoutSeconds { get; set; } = 30;
        public string OutboundTrunk { get; set; }
        public string NormalizedCallerNumber { get; set; } = string.Empty;
        public string DecisionId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reorder item for updating priority of rules via Drag & Drop.
    /// </summary>
    public class CallRouteOrderItem
    {
        public string Id { get; set; }
        public int Priority { get; set; }
    }

    /// <summary>
    /// Standard interface that all smart routing scenarios must implement.
    /// </summary>
    public interface ISmartRouteScenario
    {
        string ScenarioId { get; }
        string Title { get; }
        string Description { get; }
        int Priority { get; }
        bool IsEnabled { get; set; }
    }
}
