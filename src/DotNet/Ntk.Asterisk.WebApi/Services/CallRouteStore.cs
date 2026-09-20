using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Ntk.Asterisk.WebApi.Contracts;
using Ntk.Asterisk.WebApi.Hubs;

namespace Ntk.Asterisk.WebApi.Services;

public interface ICallRouteStore
{
    IReadOnlyList<CallRouteDto> GetList(string? quickSearch = null, bool? isActive = null);
    CallRouteDto? GetById(string id);
    CallRouteDto Upsert(CallRouteUpsertRequest request);
    bool Delete(string id);
    CallRouteDto? ToggleActive(string id);
    bool Reorder(List<CallRouteOrderItemDto> items);
    CallRouteLookupResultDto Lookup(string callerNumber);
    CallRouteLookupResultDto Lookup(CallRouteLookupRequest request);
    IReadOnlyList<SmartRouteDecisionDto> GetRecentDecisions(int limit = 50);
    SmartRouteDecisionDto ReportDecision(SmartRouteReportRequest request);
}

public sealed class CallRouteRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CallerNumber { get; set; } = string.Empty;
    public string NormalizedCallerNumber { get; set; } = string.Empty;
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

internal sealed class CallRoutesDocument
{
    public int Version { get; set; } = 1;
    public List<CallRouteRecord> Routes { get; set; } = new();
}

public sealed class CallRouteStore : ICallRouteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly IHubContext<AsteriskHub> _hub;
    private readonly ILogger<CallRouteStore> _logger;
    private List<CallRouteRecord> _items;
    private readonly List<SmartRouteDecisionDto> _decisions = new();
    private const int MaxDecisionsCapacity = 100;

    public CallRouteStore(IHostEnvironment env, IHubContext<AsteriskHub> hub, ILogger<CallRouteStore> logger)
    {
        _logger = logger;
        _hub = hub;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "call-routes.json");
        _items = Load();
        SeedDefaultRoutesIfEmpty();
    }

    public IReadOnlyList<CallRouteDto> GetList(string? quickSearch = null, bool? isActive = null)
    {
        lock (_gate)
        {
            IEnumerable<CallRouteRecord> query = _items;

            if (isActive.HasValue)
            {
                query = query.Where(r => r.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(quickSearch))
            {
                var term = quickSearch.Trim().ToLowerInvariant();
                var normTerm = NormalizePhoneNumber(term);
                query = query.Where(r =>
                    (r.CallerNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.NormalizedCallerNumber?.Contains(normTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.ContactName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.TargetExtension?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.TargetExternalNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return query
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.UpdatedAt)
                .Select(ToDto)
                .ToList();
        }
    }

    public CallRouteDto? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate)
        {
            var record = _items.FirstOrDefault(r => string.Equals(r.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            return record != null ? ToDto(record) : null;
        }
    }

    public CallRouteDto Upsert(CallRouteUpsertRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.CallerNumber))
            throw new ArgumentException("Caller number is required.", nameof(request.CallerNumber));

        var cleanCaller = request.CallerNumber.Trim();
        var normCaller = NormalizePhoneNumber(cleanCaller);

        lock (_gate)
        {
            CallRouteRecord? record = null;
            if (!string.IsNullOrWhiteSpace(request.Id))
            {
                record = _items.FirstOrDefault(r => string.Equals(r.Id, request.Id.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (record == null)
            {
                record = new CallRouteRecord
                {
                    Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _items.Add(record);
            }

            record.CallerNumber = cleanCaller;
            record.NormalizedCallerNumber = normCaller;
            record.ContactName = request.ContactName?.Trim();
            record.Description = request.Description?.Trim();
            record.TargetExtension = request.TargetExtension?.Trim();
            record.TargetExternalNumber = request.TargetExternalNumber?.Trim();
            record.ExtensionTimeout = request.ExtensionTimeout is >= 5 and <= 120 ? request.ExtensionTimeout.Value : 15;
            record.ExternalTimeout = request.ExternalTimeout is >= 5 and <= 120 ? request.ExternalTimeout.Value : 30;
            record.OutboundTrunk = request.OutboundTrunk?.Trim();
            record.IsActive = request.IsActive ?? true;
            record.Priority = request.Priority ?? 1;
            record.UpdatedAt = DateTimeOffset.UtcNow;

            PersistUnlocked();
            _logger.LogInformation("Saved Smart Call Route {Id} for caller {Caller} (Priority: {Priority})", record.Id, record.CallerNumber, record.Priority);
            return ToDto(record);
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_gate)
        {
            var removed = _items.RemoveAll(r => string.Equals(r.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                PersistUnlocked();
                _logger.LogInformation("Deleted Smart Call Route {Id}", id);
            }
            return removed;
        }
    }

    public CallRouteDto? ToggleActive(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate)
        {
            var record = _items.FirstOrDefault(r => string.Equals(r.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            if (record == null) return null;

            record.IsActive = !record.IsActive;
            record.UpdatedAt = DateTimeOffset.UtcNow;
            PersistUnlocked();
            _logger.LogInformation("Toggled Smart Call Route {Id} active status to {Status}", record.Id, record.IsActive);
            return ToDto(record);
        }
    }

    public bool Reorder(List<CallRouteOrderItemDto> items)
    {
        if (items == null || items.Count == 0) return false;
        lock (_gate)
        {
            var modified = false;
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item?.Id)) continue;
                var record = _items.FirstOrDefault(r => string.Equals(r.Id, item.Id.Trim(), StringComparison.OrdinalIgnoreCase));
                if (record != null && record.Priority != item.Priority)
                {
                    record.Priority = item.Priority;
                    record.UpdatedAt = DateTimeOffset.UtcNow;
                    modified = true;
                }
            }

            if (modified)
            {
                PersistUnlocked();
                _logger.LogInformation("Reordered {Count} smart call routes priority via Drag & Drop", items.Count);
            }
            return true;
        }
    }

    public CallRouteLookupResultDto Lookup(string callerNumber)
    {
        return Lookup(new CallRouteLookupRequest { CallerNumber = callerNumber, Source = "manual" });
    }

    public CallRouteLookupResultDto Lookup(CallRouteLookupRequest request)
    {
        var callerNumber = request?.CallerNumber ?? string.Empty;
        var channel = request?.Channel?.Trim();
        var uniqueId = request?.UniqueId?.Trim();
        var source = request?.Source?.Trim() ?? "fastagi";

        if (string.IsNullOrWhiteSpace(callerNumber))
        {
            return new CallRouteLookupResultDto
            {
                Matched = false,
                Action = "Fallback",
                Message = "No caller number provided."
            };
        }

        var normalized = NormalizePhoneNumber(callerNumber);
        lock (_gate)
        {
            var activeRoutes = _items.Where(r => r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.UpdatedAt)
                .ToList();

            // Find all matching rules for this caller (exact or suffix)
            var matchingRules = activeRoutes.Where(r =>
                string.Equals(r.NormalizedCallerNumber, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.CallerNumber, callerNumber.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (normalized.Length >= 7 && !string.IsNullOrWhiteSpace(r.NormalizedCallerNumber) &&
                 r.NormalizedCallerNumber.EndsWith(normalized.Length > 10 ? normalized[^10..] : normalized, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            if (matchingRules.Count == 0)
            {
                var fallbackDecision = RecordDecisionUnlocked(new SmartRouteDecisionDto
                {
                    CallerNumber = callerNumber,
                    NormalizedCallerNumber = normalized,
                    Channel = channel,
                    UniqueId = uniqueId,
                    Source = source,
                    Matched = false,
                    Action = "Fallback",
                    Reason = $"شماره {callerNumber} در لیست قوانین تعریف نشده است -> انتقال به ادامه منوی صوتی IVR",
                    Status = "Fallback"
                });

                return new CallRouteLookupResultDto
                {
                    Matched = false,
                    NormalizedCallerNumber = normalized,
                    Action = "Fallback",
                    Message = "No matching active call route found.",
                    Decision = fallbackDecision
                };
            }

            var primary = matchingRules[0];
            var steps = new List<SmartRouteStepDto>();
            for (var i = 0; i < matchingRules.Count; i++)
            {
                var r = matchingRules[i];
                steps.Add(new SmartRouteStepDto
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

            var hasExtension = !string.IsNullOrWhiteSpace(primary.TargetExtension);
            var hasExternal = !string.IsNullOrWhiteSpace(primary.TargetExternalNumber);

            string action;
            string reasonText;
            if (hasExtension && hasExternal)
            {
                action = "DialExtensionThenForward";
                reasonText = $"مسیریابی هوشمند برای {primary.ContactName ?? primary.CallerNumber}: ابتدا داخلی {primary.TargetExtension} ({primary.ExtensionTimeout}s)، در صورت عدم پاسخ انتقال به موبایل {primary.TargetExternalNumber} ({primary.ExternalTimeout}s)";
            }
            else if (hasExtension)
            {
                action = "DialExtensionOnly";
                reasonText = $"مسیریابی هوشمند برای {primary.ContactName ?? primary.CallerNumber}: اتصال مستقیم به داخلی {primary.TargetExtension} ({primary.ExtensionTimeout}s)";
            }
            else if (hasExternal)
            {
                action = "DialExternalOnly";
                reasonText = $"مسیریابی هوشمند برای {primary.ContactName ?? primary.CallerNumber}: انتقال مستقیم به شماره همراه {primary.TargetExternalNumber} ({primary.ExternalTimeout}s)";
            }
            else
            {
                action = "Fallback";
                reasonText = $"قانون هوشمند برای {primary.ContactName ?? primary.CallerNumber} بدون مقصد تنظیم شده است -> بازگشت به IVR";
            }

            var decision = RecordDecisionUnlocked(new SmartRouteDecisionDto
            {
                CallerNumber = callerNumber,
                NormalizedCallerNumber = normalized,
                ContactName = primary.ContactName,
                Channel = channel,
                UniqueId = uniqueId,
                Source = source,
                Matched = true,
                Action = action,
                DestinationExtension = primary.TargetExtension,
                DestinationExternalNumber = primary.TargetExternalNumber,
                OutboundTrunk = primary.OutboundTrunk,
                TimeoutSeconds = primary.ExtensionTimeout > 0 ? primary.ExtensionTimeout : 15,
                ExtensionTimeoutSeconds = primary.ExtensionTimeout > 0 ? primary.ExtensionTimeout : 15,
                ExternalTimeoutSeconds = primary.ExternalTimeout > 0 ? primary.ExternalTimeout : 30,
                Reason = reasonText,
                MatchedRuleId = primary.Id,
                Status = "Routed"
            });

            return new CallRouteLookupResultDto
            {
                Matched = true,
                Route = ToDto(primary),
                Steps = steps,
                Action = action,
                DestinationExtension = primary.TargetExtension,
                DestinationExternalNumber = primary.TargetExternalNumber,
                TimeoutSeconds = primary.ExtensionTimeout > 0 ? primary.ExtensionTimeout : 15,
                ExtensionTimeoutSeconds = primary.ExtensionTimeout > 0 ? primary.ExtensionTimeout : 15,
                ExternalTimeoutSeconds = primary.ExternalTimeout > 0 ? primary.ExternalTimeout : 30,
                OutboundTrunk = primary.OutboundTrunk,
                NormalizedCallerNumber = normalized,
                Message = $"Matched {matchingRules.Count} route(s) for {primary.ContactName ?? primary.CallerNumber}: Action={action}",
                Decision = decision
            };
        }
    }

    public IReadOnlyList<SmartRouteDecisionDto> GetRecentDecisions(int limit = 50)
    {
        var bounded = Math.Clamp(limit, 1, 100);
        lock (_gate)
        {
            return _decisions.Take(bounded).ToList();
        }
    }

    public SmartRouteDecisionDto ReportDecision(SmartRouteReportRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        lock (_gate)
        {
            SmartRouteDecisionDto? decision = null;
            if (!string.IsNullOrWhiteSpace(request.DecisionId))
            {
                decision = _decisions.FirstOrDefault(d => string.Equals(d.Id, request.DecisionId.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (decision != null)
            {
                decision.Status = request.Status;
                if (!string.IsNullOrWhiteSpace(request.Note)) decision.Note = request.Note;
                if (!string.IsNullOrWhiteSpace(request.Channel)) decision.Channel = request.Channel;
                BroadcastDecision(decision);
                return decision;
            }

            decision = new SmartRouteDecisionDto
            {
                Id = !string.IsNullOrWhiteSpace(request.DecisionId) ? request.DecisionId.Trim() : Guid.NewGuid().ToString("N"),
                CallerNumber = request.CallerNumber,
                NormalizedCallerNumber = NormalizePhoneNumber(request.CallerNumber),
                Channel = request.Channel,
                UniqueId = request.UniqueId,
                Action = request.Action ?? "Direct",
                DestinationExtension = request.Target,
                Status = request.Status,
                Note = request.Note,
                Reason = request.Note ?? string.Empty
            };

            return RecordDecisionUnlocked(decision);
        }
    }

    private SmartRouteDecisionDto RecordDecisionUnlocked(SmartRouteDecisionDto decision)
    {
        _decisions.Insert(0, decision);
        while (_decisions.Count > MaxDecisionsCapacity)
        {
            _decisions.RemoveAt(_decisions.Count - 1);
        }
        BroadcastDecision(decision);
        return decision;
    }

    private void BroadcastDecision(SmartRouteDecisionDto decision)
    {
        try
        {
            _ = _hub.Clients.Group("monitor").SendAsync("smartRouteDecision", decision);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to broadcast smartRouteDecision over SignalR");
        }
    }

    public static string NormalizePhoneNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var chars = raw.Trim().Select(c => c switch
        {
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            _ => c
        }).Where(c => char.IsDigit(c) || c == '+').ToArray();

        var text = new string(chars);

        if (text.StartsWith("+98", StringComparison.Ordinal))
        {
            text = "0" + text[3..];
        }
        else if (text.StartsWith("0098", StringComparison.Ordinal))
        {
            text = "0" + text[4..];
        }
        else if (text.StartsWith("98", StringComparison.Ordinal) && text.Length == 12)
        {
            text = "0" + text[2..];
        }
        else if (text.StartsWith("9", StringComparison.Ordinal) && text.Length == 10)
        {
            text = "0" + text;
        }

        return text.Replace("+", string.Empty);
    }

    private List<CallRouteRecord> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<CallRouteRecord>();
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<CallRoutesDocument>(json, JsonOptions);
            return doc?.Routes ?? new List<CallRouteRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load smart call routes from {Path}", _path);
            return new List<CallRouteRecord>();
        }
    }

    private void PersistUnlocked()
    {
        try
        {
            var doc = new CallRoutesDocument { Routes = _items };
            var json = JsonSerializer.Serialize(doc, JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist smart call routes to {Path}", _path);
        }
    }

    private void SeedDefaultRoutesIfEmpty()
    {
        lock (_gate)
        {
            if (_items.Count > 0) return;

            _items.Add(new CallRouteRecord
            {
                Id = "9aed3e3a55cb4e3f9d16362bbb1dd40f",
                CallerNumber = "09125210076",
                NormalizedCallerNumber = "09125210076",
                ContactName = "علیرضا کاروی",
                Description = "مسیر هوشمند پیش‌فرض مدیریت",
                TargetExtension = "94",
                TargetExternalNumber = "09131183892",
                ExtensionTimeout = 15,
                ExternalTimeout = 30,
                IsActive = true,
                Priority = 10,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            PersistUnlocked();
            _logger.LogInformation("Seeded default smart call route for 09125210076");
        }
    }

    private static CallRouteDto ToDto(CallRouteRecord r) => new()
    {
        Id = r.Id,
        CallerNumber = r.CallerNumber,
        ContactName = r.ContactName,
        Description = r.Description,
        TargetExtension = r.TargetExtension,
        TargetExternalNumber = r.TargetExternalNumber,
        ExtensionTimeout = r.ExtensionTimeout,
        ExternalTimeout = r.ExternalTimeout,
        OutboundTrunk = r.OutboundTrunk,
        IsActive = r.IsActive,
        Priority = r.Priority,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
