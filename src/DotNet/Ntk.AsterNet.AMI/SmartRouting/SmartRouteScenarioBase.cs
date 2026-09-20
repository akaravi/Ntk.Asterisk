using System;
using System.Collections.Generic;
using System.Linq;

namespace Ntk.AsterNet.AMI.SmartRouting
{
    /// <summary>
    /// Base class for all Smart Call Routing scenarios.
    /// Provides shared methods for:
    /// - Normalizing phone numbers (Persian/Arabic digits, international prefixes)
    /// - Multi-rule evaluation per caller with priority cascading steps
    /// - Internal extension priority with independent ExtensionTimeout
    /// - Forwarding to external mobile with independent ExternalTimeout
    /// - Fallback to normal IVR flow on unanswered calls
    /// - Constructing Asterisk Dial strings and target delivery paths
    /// </summary>
    public abstract class SmartRouteScenarioBase : ISmartRouteScenario
    {
        public abstract string ScenarioId { get; }
        public abstract string Title { get; }
        public abstract string Description { get; }
        public virtual int Priority => 1;
        public virtual bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Normalizes phone numbers across Iranian and international variations.
        /// Shared across all scenarios.
        /// </summary>
        public static string NormalizePhoneNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var chars = new List<char>();
            foreach (var c in raw.Trim())
            {
                if (c >= '۰' && c <= '۹')
                {
                    chars.Add((char)('0' + (c - '۰')));
                }
                else if (c >= '٠' && c <= '٩')
                {
                    chars.Add((char)('0' + (c - '٠')));
                }
                else if (char.IsDigit(c) || c == '+')
                {
                    chars.Add(c);
                }
            }

            var text = new string(chars.ToArray());

            if (text.StartsWith("+98"))
            {
                text = "0" + text.Substring(3);
            }
            else if (text.StartsWith("0098"))
            {
                text = "0" + text.Substring(4);
            }
            else if (text.StartsWith("98") && text.Length == 12)
            {
                text = "0" + text.Substring(2);
            }
            else if (text.StartsWith("9") && text.Length == 10)
            {
                text = "0" + text;
            }

            return text.Replace("+", string.Empty);
        }

        /// <summary>
        /// Evaluates a caller number against all active routing rules.
        /// Supports multiple routes per number, returning the full ordered cascade of matching steps.
        /// </summary>
        public virtual SmartRouteEvaluationResult EvaluateRules(
            IEnumerable<SmartRouteRuleItem> rules,
            string callerNumber)
        {
            if (string.IsNullOrWhiteSpace(callerNumber))
            {
                return new SmartRouteEvaluationResult
                {
                    Matched = false,
                    Action = "Fallback",
                    Reason = "Caller number is empty"
                };
            }

            var normalized = NormalizePhoneNumber(callerNumber);
            var activeRules = (rules ?? Enumerable.Empty<SmartRouteRuleItem>())
                .Where(r => r != null && r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.UpdatedAt)
                .ToList();

            // Find all matching rules for this caller (exact or suffix)
            var matchingRules = activeRules.Where(r =>
                string.Equals(r.NormalizedCallerNumber, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.CallerNumber, callerNumber.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (normalized.Length >= 7 && !string.IsNullOrWhiteSpace(r.NormalizedCallerNumber) &&
                 r.NormalizedCallerNumber.EndsWith(normalized.Length > 10 ? normalized.Substring(normalized.Length - 10) : normalized, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            if (matchingRules.Count == 0)
            {
                return new SmartRouteEvaluationResult
                {
                    Matched = false,
                    NormalizedCallerNumber = normalized,
                    Action = "Fallback",
                    Reason = $"No matching active smart route rule for {callerNumber}"
                };
            }

            var primary = matchingRules[0];
            var steps = new List<SmartRouteStepItem>();
            for (var i = 0; i < matchingRules.Count; i++)
            {
                var r = matchingRules[i];
                steps.Add(new SmartRouteStepItem
                {
                    StepOrder = i + 1,
                    RuleId = r.Id,
                    ContactName = r.ContactName,
                    DestinationExtension = r.TargetExtension,
                    DestinationExternalNumber = r.TargetExternalNumber,
                    ExtensionTimeoutSeconds = r.ExtensionTimeout > 0 ? r.ExtensionTimeout : 15,
                    ExternalTimeoutSeconds = r.ExternalTimeout > 0 ? r.ExternalTimeout : 30,
                    OutboundTrunk = r.OutboundTrunk,
                    Priority = r.Priority
                });
            }

            var hasExt = !string.IsNullOrWhiteSpace(primary.TargetExtension);
            var hasMob = !string.IsNullOrWhiteSpace(primary.TargetExternalNumber);

            string action;
            if (hasExt && hasMob) action = "DialExtensionThenForward";
            else if (hasExt) action = "DialExtensionOnly";
            else if (hasMob) action = "DialExternalOnly";
            else action = "Fallback";

            return new SmartRouteEvaluationResult
            {
                Matched = true,
                MatchedRule = primary,
                Steps = steps,
                Action = action,
                DestinationExtension = primary.TargetExtension,
                DestinationExternalNumber = primary.TargetExternalNumber,
                ExtensionTimeoutSeconds = primary.ExtensionTimeout > 0 ? primary.ExtensionTimeout : 15,
                ExternalTimeoutSeconds = primary.ExternalTimeout > 0 ? primary.ExternalTimeout : 30,
                OutboundTrunk = primary.OutboundTrunk,
                NormalizedCallerNumber = normalized,
                Reason = $"Matched {matchingRules.Count} rule(s) for {primary.ContactName ?? primary.CallerNumber} (Primary Priority {primary.Priority}): {action}"
            };
        }

        /// <summary>
        /// Builds Asterisk Dial string for internal extension or external mobile number.
        /// </summary>
        public virtual string BuildDialString(string target, string outboundTrunk, int timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(target)) return string.Empty;

            var cleanTarget = target.Trim();
            var timeout = timeoutSeconds > 0 ? timeoutSeconds : 15;

            // If specific trunk is provided (not default)
            if (!string.IsNullOrWhiteSpace(outboundTrunk) &&
                !string.Equals(outboundTrunk, "trunk-default", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(outboundTrunk, "from-internal", StringComparison.OrdinalIgnoreCase))
            {
                return $"PJSIP/{cleanTarget}@{outboundTrunk},{timeout},tTkK";
            }

            // Standard FreePBX / Asterisk internal route delivery
            return $"Local/{cleanTarget}@from-internal,{timeout},tTkK";
        }
    }
}
