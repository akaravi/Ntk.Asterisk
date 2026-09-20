using System;
using System.Threading.Tasks;
using Ntk.AsterNet.AMI.Manager.Action;
using Ntk.AsterNet.AMI.SmartRouting;
using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services.SmartRouting
{
    /// <summary>
    /// Scenario 2: Live In-IVR AMI Intercept Scenario.
    /// Monitors active Asterisk channels entering IVR prompts, evaluates smart routing rules,
    /// and redirects matching callers out of IVR into the smart routing execution engine
    /// (custom-smartroute-after-tc) which executes:
    /// 1. Internal extension with ExtensionTimeout
    /// 2. External mobile with independent ExternalTimeout
    /// 3. Multi-rule cascading steps
    /// 4. Clean fallback to IVR if all unanswered
    /// </summary>
    public class Scenario2_LiveIvrAmiScenario : SmartRouteScenarioBase
    {
        private readonly IAmiSession _ami;
        private readonly ICallRouteStore _routeStore;
        private readonly ILogger<Scenario2_LiveIvrAmiScenario> _logger;

        public override string ScenarioId => "scenario-2-live-ivr-ami";
        public override string Title => "سناریوی ۲: مانیتورینگ زنده منوی صوتی (Live In-IVR AMI Intercept)";
        public override string Description => "تماس در حین پخش پیام IVR و انتخاب منو مانیتور شده و در صورت انطباق با قوانین هوشمند بلافاصله از IVR خارج و به فرآیند انتقال هوشمند هدایت می‌شود.";
        public override int Priority => 2;

        public Scenario2_LiveIvrAmiScenario(
            IAmiSession ami,
            ICallRouteStore routeStore,
            ILogger<Scenario2_LiveIvrAmiScenario> logger)
        {
            _ami = ami ?? throw new ArgumentNullException(nameof(ami));
            _routeStore = routeStore ?? throw new ArgumentNullException(nameof(routeStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Evaluates the channel entering IVR and executes live redirection if matched.
        /// </summary>
        public async Task<bool> ExecuteLiveIvrInterceptAsync(string channel, string context, string uniqueId, string callerId)
        {
            try
            {
                var lookupReq = new CallRouteLookupRequest
                {
                    CallerNumber = callerId,
                    Channel = channel,
                    UniqueId = uniqueId,
                    Source = ScenarioId
                };

                var lookup = _routeStore.Lookup(lookupReq);
                if (!lookup.Matched || lookup.Route == null)
                {
                    _logger.LogInformation("AMI Live IVR: Caller {Caller} on channel {Channel} has no matching smart route -> continuing normal IVR", callerId, channel);
                    return false;
                }

                var target = !string.IsNullOrWhiteSpace(lookup.DestinationExtension)
                    ? lookup.DestinationExtension.Trim()
                    : lookup.DestinationExternalNumber?.Trim();

                if (string.IsNullOrWhiteSpace(target))
                {
                    _logger.LogWarning("AMI Live IVR: Matched route for {Caller} has no target destination", callerId);
                    return false;
                }

                _logger.LogInformation(
                    "AMI Live IVR Match! Redirecting active channel {Channel} (Caller: {Caller}) out of {Context} -> from-internal to target {Target}",
                    channel, callerId, context, target);

                var redirect = new RedirectAction(channel, "from-internal", target, 1);
                var response = await _ami.SendActionAsync(redirect).ConfigureAwait(false);
                var isSuccess = response?.IsSuccess() ?? false;
                var outcomeStatus = isSuccess ? "Redirected" : "RedirectFailed";
                var note = isSuccess
                    ? $"کانال {channel} با موفقیت در حین پخش منوی صوتی قطع و به مسیر هوشمند (داخلی {lookup.DestinationExtension} / همراه {lookup.DestinationExternalNumber}) هدایت شد"
                    : $"خطا در انتقال کانال {channel}: {response?.Message}";

                _logger.LogInformation("AMI Live IVR Redirect Result for {Channel}: {Status} - {Note}", channel, outcomeStatus, note);

                _routeStore.ReportDecision(new SmartRouteReportRequest
                {
                    DecisionId = lookup.Decision?.Id,
                    CallerNumber = callerId,
                    Channel = channel,
                    UniqueId = uniqueId,
                    Action = lookup.Action,
                    Target = target,
                    Status = outcomeStatus,
                    Note = note
                });

                return isSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Scenario 2 Live IVR Redirect for channel {Channel}", channel);
                return false;
            }
        }
    }
}
