using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhoneCdrStore
{
    (IReadOnlyList<WebPhoneCdrDto> Items, int TotalCount) GetList(
        int pageIndex,
        int pageSize,
        string? sortBy,
        string? sortDir,
        string? quickSearch);
    WebPhoneCdrDto Add(WebPhoneCdrAddRequest request);
}

internal sealed class WebPhoneCdrRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? BuddyId { get; set; }
    public string? Direction { get; set; }
    public string? WithNumber { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Disposition { get; set; }
    public string? RecordingId { get; set; }
    public string? Notes { get; set; }
}

internal sealed class WebPhoneCdrDocument
{
    public int Version { get; set; } = 1;
    public List<WebPhoneCdrRecord> Records { get; set; } = new();
}

public sealed class WebPhoneCdrStore : IWebPhoneCdrStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<WebPhoneCdrStore> _logger;
    private List<WebPhoneCdrRecord> _items;

    public WebPhoneCdrStore(IHostEnvironment env, ILogger<WebPhoneCdrStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "webphone-cdr.json");
        _items = Load();
    }

    public (IReadOnlyList<WebPhoneCdrDto> Items, int TotalCount) GetList(
        int pageIndex,
        int pageSize,
        string? sortBy,
        string? sortDir,
        string? quickSearch)
    {
        lock (_gate)
        {
            IEnumerable<WebPhoneCdrRecord> q = _items;
            if (!string.IsNullOrWhiteSpace(quickSearch))
            {
                var s = quickSearch.Trim();
                q = q.Where(c =>
                    (c.WithNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.DisplayName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.Disposition?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.Direction?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            q = (sortBy ?? "startedAtUtc").Trim().ToLowerInvariant() switch
            {
                "withnumber" or "number" => desc
                    ? q.OrderByDescending(c => c.WithNumber)
                    : q.OrderBy(c => c.WithNumber),
                "duration" or "durationseconds" => desc
                    ? q.OrderByDescending(c => c.DurationSeconds)
                    : q.OrderBy(c => c.DurationSeconds),
                "disposition" => desc
                    ? q.OrderByDescending(c => c.Disposition)
                    : q.OrderBy(c => c.Disposition),
                _ => desc
                    ? q.OrderByDescending(c => c.StartedAtUtc)
                    : q.OrderBy(c => c.StartedAtUtc)
            };

            var list = q.ToList();
            var size = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);
            var index = pageIndex < 0 ? 0 : pageIndex;
            var page = list.Skip(index * size).Take(size).Select(ToDto).ToList();
            return (page, list.Count);
        }
    }

    public WebPhoneCdrDto Add(WebPhoneCdrAddRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var started = request.StartedAtUtc ?? DateTimeOffset.UtcNow;
            var ended = request.EndedAtUtc;
            int? duration = request.DurationSeconds;
            if (duration is null && ended is not null)
                duration = (int)Math.Max(0, (ended.Value - started).TotalSeconds);

            var record = new WebPhoneCdrRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                BuddyId = NullIfWhiteSpace(request.BuddyId),
                Direction = NullIfWhiteSpace(request.Direction),
                WithNumber = NullIfWhiteSpace(request.WithNumber),
                DisplayName = NullIfWhiteSpace(request.DisplayName),
                StartedAtUtc = started,
                EndedAtUtc = ended,
                DurationSeconds = duration,
                Disposition = NullIfWhiteSpace(request.Disposition),
                RecordingId = NullIfWhiteSpace(request.RecordingId),
                Notes = NullIfWhiteSpace(request.Notes)
            };
            _items.Add(record);
            PersistUnlocked();
            return ToDto(record);
        }
    }

    private List<WebPhoneCdrRecord> Load()
    {
        if (!File.Exists(_path))
            return new List<WebPhoneCdrRecord>();
        try
        {
            var doc = JsonSerializer.Deserialize<WebPhoneCdrDocument>(File.ReadAllText(_path), JsonOptions);
            return doc?.Records ?? new List<WebPhoneCdrRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load webphone-cdr");
            return new List<WebPhoneCdrRecord>();
        }
    }

    private void PersistUnlocked()
    {
        // Cap growth for Wave 2 file store
        if (_items.Count > 5000)
            _items = _items.OrderByDescending(c => c.StartedAtUtc).Take(4000).ToList();

        var json = JsonSerializer.Serialize(new WebPhoneCdrDocument { Records = _items.ToList() }, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    private static WebPhoneCdrDto ToDto(WebPhoneCdrRecord c) => new()
    {
        Id = c.Id,
        BuddyId = c.BuddyId,
        Direction = c.Direction,
        WithNumber = c.WithNumber,
        DisplayName = c.DisplayName,
        StartedAtUtc = c.StartedAtUtc,
        EndedAtUtc = c.EndedAtUtc,
        DurationSeconds = c.DurationSeconds,
        Disposition = c.Disposition,
        RecordingId = c.RecordingId,
        Notes = c.Notes
    };

    private static string? NullIfWhiteSpace(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
