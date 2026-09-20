using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Ntk.AsterNet.AMI.FastAGI;
using Ntk.AsterNet.AMI.FastAGI.Command;
using Ntk.AsterNet.AMI.SmartRouting;

namespace Asterisk.Console.AMI
{
    /// <summary>
    /// Scenario 1: Direct Inbound FastAGI Smart Call Routing.
    /// Executed after Time Conditions (or directly on DID Inbound Route).
    /// Inherits from SmartRouteScenarioBase to share normalization, multi-step rule cascade, and dial string generation.
    /// </summary>
    public class Scenario1_DirectFastAgiScenario : SmartRouteScenarioBase
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public override string ScenarioId => "scenario-1-direct-fastagi";
        public override string Title => "سناریوی ۱: مسیریابی مستقیم ورودی (Direct Inbound FastAGI)";
        public override string Description => "تماس پس از شرایط زمانی مستقیماً به سیستم هوشمند متصل شده و در صورت عدم تطابق به منوی IVR بازمی‌گردد.";
        public override int Priority => 1;

        public string ApiBaseUrl { get; set; } = "http://127.0.0.1:5310";

        /// <summary>
        /// Executes the FastAGI call routing flow for Scenario 1.
        /// Priority sequence:
        /// 1. Dial Internal Extension (ExtensionTimeoutSeconds, e.g. 15s)
        /// 2. If unanswered/busy -> Forward to External Mobile (ExternalTimeoutSeconds, e.g. 30s)
        /// 3. If multiple rules exist -> Cascade to next rule in priority order
        /// 4. If all unanswered -> Return to dialplan / normal IVR flow
        /// </summary>
        public virtual void ExecuteAgiFlow(AGIRequest request, AGIChannel channel)
        {
            var callerNumber = request.CallerId ?? string.Empty;
            var callerName = request.CallerIdName;
            var channelName = request.Channel ?? string.Empty;
            var uniqueId = request.UniqueId ?? string.Empty;
            var context = request.Context;
            var exten = request.Extension;
            var priority = request.Priority;
            var packetId = Guid.NewGuid().ToString("N");

            var port = 4573;
            if (!string.IsNullOrWhiteSpace(request.RequestURL))
            {
                if (Uri.TryCreate(request.RequestURL, UriKind.Absolute, out var reqUri) && reqUri.Port > 0)
                {
                    port = reqUri.Port;
                }
                else if (request.RequestURL.Contains(":4572"))
                {
                    port = 4572;
                }
            }

            // Extract headers
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request.Request != null)
            {
                foreach (System.Collections.DictionaryEntry entry in request.Request)
                {
                    if (entry.Key != null && entry.Value != null)
                    {
                        headers[entry.Key.ToString()] = entry.Value.ToString();
                    }
                }
            }

            // 1. Report packet telemetry
            ReportPacketTelemetry(packetId, port, "smartroute", callerNumber, callerName, channelName, uniqueId, context, exten, priority, headers, null, "Processing", "AGI request received from Asterisk");

            if (string.IsNullOrWhiteSpace(callerNumber))
            {
                ReportPacketTelemetry(packetId, port, "smartroute", callerNumber, callerName, channelName, uniqueId, context, exten, priority, null, "Return", "Completed", "Caller number empty -> Return to dialplan");
                return;
            }

            string decisionId = null;
            try
            {
                var lookupUrl = $"{ApiBaseUrl.TrimEnd('/')}/api/v1/CallRoutes/Lookup";
                var payload = new
                {
                    callerNumber = callerNumber,
                    channel = channelName,
                    uniqueId = uniqueId,
                    source = ScenarioId,
                    dnis = request.Extension
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var httpResponse = HttpClient.PostAsync(lookupUrl, content).GetAwaiter().GetResult();
                var response = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                var isSuccess = root.TryGetProperty("isSuccess", out var successProp) && successProp.GetBoolean();

                if (!isSuccess || !root.TryGetProperty("data", out var dataProp) || dataProp.GetArrayLength() == 0)
                {
                    return;
                }

                var item = dataProp[0];
                if (item.TryGetProperty("decision", out var decProp) && decProp.ValueKind == JsonValueKind.Object)
                {
                    if (decProp.TryGetProperty("id", out var idProp))
                    {
                        decisionId = idProp.GetString();
                    }
                }

                var matched = item.TryGetProperty("matched", out var matchedProp) && matchedProp.GetBoolean();
                if (!matched)
                {
                    ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "Fallback", null, "Fallback", "انتقال به برنامه اصلی به دلیل عدم انطباق با قوانین");
                    return;
                }

                // Extract cascading steps if present, otherwise fall back to primary route
                var steps = new List<SmartRouteStepItem>();
                if (item.TryGetProperty("steps", out var stepsProp) && stepsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in stepsProp.EnumerateArray())
                    {
                        steps.Add(new SmartRouteStepItem
                        {
                            RuleId = s.TryGetProperty("ruleId", out var rId) ? rId.GetString() : string.Empty,
                            ContactName = s.TryGetProperty("contactName", out var cName) ? cName.GetString() : null,
                            DestinationExtension = s.TryGetProperty("destinationExtension", out var dExt) ? dExt.GetString() : null,
                            DestinationExternalNumber = s.TryGetProperty("destinationExternalNumber", out var dMob) ? dMob.GetString() : null,
                            ExtensionTimeoutSeconds = s.TryGetProperty("extensionTimeoutSeconds", out var eTo) ? eTo.GetInt32() : 15,
                            ExternalTimeoutSeconds = s.TryGetProperty("externalTimeoutSeconds", out var xTo) ? xTo.GetInt32() : 30,
                            OutboundTrunk = s.TryGetProperty("outboundTrunk", out var oTrk) ? oTrk.GetString() : null
                        });
                    }
                }

                if (steps.Count == 0)
                {
                    steps.Add(new SmartRouteStepItem
                    {
                        DestinationExtension = item.TryGetProperty("destinationExtension", out var extP) ? extP.GetString() : null,
                        DestinationExternalNumber = item.TryGetProperty("destinationExternalNumber", out var mobP) ? mobP.GetString() : null,
                        ExtensionTimeoutSeconds = item.TryGetProperty("extensionTimeoutSeconds", out var eToP) ? eToP.GetInt32() : 15,
                        ExternalTimeoutSeconds = item.TryGetProperty("externalTimeoutSeconds", out var xToP) ? xToP.GetInt32() : 30,
                        OutboundTrunk = item.TryGetProperty("outboundTrunk", out var trkP) ? trkP.GetString() : null
                    });
                }

                // Execute cascading steps in order
                for (var i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    var ext = step.DestinationExtension?.Trim();
                    var mobile = step.DestinationExternalNumber?.Trim();
                    var extTimeout = step.ExtensionTimeoutSeconds > 0 ? step.ExtensionTimeoutSeconds : 15;
                    var mobTimeout = step.ExternalTimeoutSeconds > 0 ? step.ExternalTimeoutSeconds : 30;
                    var trunk = step.OutboundTrunk;

                    // Step A: Priority 1 - Dial Extension first if defined
                    if (!string.IsNullOrWhiteSpace(ext))
                    {
                        var extDial = BuildDialString(ext, "from-internal", extTimeout);
                        ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "DialExtension", ext, "Dialing", $"مرحله {i + 1}: در حال زنگ خوردن داخلی {ext} (مهلت: {extTimeout}s)");
                        channel.SendCommand(new ExecCommand("Dial", extDial));
                        var dialReply = channel.SendCommand(new GetVariableCommand("DIALSTATUS"));
                        var dialStatus = dialReply?.Extra ?? string.Empty;

                        if (string.Equals(dialStatus, "ANSWER", StringComparison.OrdinalIgnoreCase))
                        {
                            channel.SendCommand(new SetVariableCommand("SMART_ROUTE_HANDLED", "1"));
                            ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "DialExtension", ext, "Answered", $"تماس توسط داخلی {ext} پاسخ داده شد");
                            return;
                        }

                        // Step B: Extension unanswered/busy -> Forward to External Mobile with independent timeout
                        if (!string.IsNullOrWhiteSpace(mobile))
                        {
                            var mobDial = BuildDialString(mobile, trunk, mobTimeout);
                            ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "ForwardMobile", mobile, "Dialing", $"مرحله {i + 1}: عدم پاسخ داخلی {ext} ({dialStatus}) -> انتقال به شماره همراه {mobile} (مهلت: {mobTimeout}s)");
                            channel.SendCommand(new ExecCommand("Dial", mobDial));
                            var mobReply = channel.SendCommand(new GetVariableCommand("DIALSTATUS"));
                            var mobStatus = mobReply?.Extra ?? string.Empty;
                            var mobAnswered = string.Equals(mobStatus, "ANSWER", StringComparison.OrdinalIgnoreCase);

                            if (mobAnswered)
                            {
                                channel.SendCommand(new SetVariableCommand("SMART_ROUTE_HANDLED", "1"));
                                ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "ForwardMobile", mobile, "Answered", $"تماس توسط شماره همراه {mobile} پاسخ داده شد");
                                return;
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(mobile))
                    {
                        // Direct mobile forward
                        var mobDial = BuildDialString(mobile, trunk, mobTimeout);
                        ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "ForwardMobile", mobile, "Dialing", $"مرحله {i + 1}: انتقال مستقیم به شماره همراه {mobile} (مهلت: {mobTimeout}s)");
                        channel.SendCommand(new ExecCommand("Dial", mobDial));
                        var mobReply = channel.SendCommand(new GetVariableCommand("DIALSTATUS"));
                        var mobStatus = mobReply?.Extra ?? string.Empty;
                        var mobAnswered = string.Equals(mobStatus, "ANSWER", StringComparison.OrdinalIgnoreCase);

                        if (mobAnswered)
                        {
                            channel.SendCommand(new SetVariableCommand("SMART_ROUTE_HANDLED", "1"));
                            ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "ForwardMobile", mobile, "Answered", $"تماس توسط شماره همراه {mobile} پاسخ داده شد");
                            return;
                        }
                    }
                }

                // If all steps and rules were unanswered -> Fallback to normal IVR flow
                ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "Fallback", null, "Fallback", "عدم پاسخ داخلی‌ها و شماره‌های همراه تعریف‌شده -> بازگشت به ادامه برنامه منوی صوتی IVR");
            }
            catch (Exception ex)
            {
                ReportOutcome(decisionId, callerNumber, channelName, uniqueId, "DefaultIVR", null, "Error", $"خطا در اجرای سناریوی ۱: {ex.Message}");
            }
        }

        private void ReportOutcome(
            string decisionId,
            string callerNumber,
            string channel,
            string uniqueId,
            string action,
            string target,
            string status,
            string note)
        {
            try
            {
                var reportUrl = $"{ApiBaseUrl.TrimEnd('/')}/api/v1/CallRoutes/ReportDecision";
                var payload = new
                {
                    decisionId = decisionId,
                    callerNumber = callerNumber,
                    channel = channel,
                    uniqueId = uniqueId,
                    action = action,
                    target = target,
                    status = status,
                    note = note
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                _ = HttpClient.PostAsync(reportUrl, content);
            }
            catch { }
        }

        private void ReportPacketTelemetry(
            string packetId,
            int port,
            string scriptName,
            string callerId,
            string callerIdName,
            string channel,
            string uniqueId,
            string context,
            string exten,
            string priority,
            Dictionary<string, string> headers,
            string command,
            string status,
            string note)
        {
            try
            {
                var telemetryUrl = $"{ApiBaseUrl.TrimEnd('/')}/api/v1/FastAgi/ReportPacket";
                var payload = new
                {
                    packetId = packetId,
                    port = port,
                    scriptName = scriptName,
                    callerId = callerId,
                    callerIdName = callerIdName,
                    channel = channel,
                    uniqueId = uniqueId,
                    context = context,
                    extension = exten,
                    priority = priority,
                    headers = headers,
                    command = command,
                    status = status,
                    note = note
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                _ = HttpClient.PostAsync(telemetryUrl, content);
            }
            catch { }
        }
    }
}
