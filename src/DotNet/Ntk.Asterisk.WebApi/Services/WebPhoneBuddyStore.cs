using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhoneBuddyStore
{
    IReadOnlyList<WebPhoneBuddyDto> GetList(string? quickSearch = null);
    WebPhoneBuddyDto? GetOne(string id);
    WebPhoneBuddyDto Add(WebPhoneBuddyUpsertRequest request);
    WebPhoneBuddyDto Update(WebPhoneBuddyUpsertRequest request);
    bool Delete(string id);
}

internal sealed class WebPhoneBuddyRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "extension";
    public string DisplayName { get; set; } = string.Empty;
    public string? ExtensionNumber { get; set; }
    public string? Description { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public string? ContactNumber1 { get; set; }
    public string? ContactNumber2 { get; set; }
    public bool Subscribe { get; set; }
    public string? SubscribeUser { get; set; }
    public bool EnableDuringDnd { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class WebPhoneBuddiesDocument
{
    public int Version { get; set; } = 1;
    public List<WebPhoneBuddyRecord> Buddies { get; set; } = new();
}

public sealed class WebPhoneBuddyStore : IWebPhoneBuddyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<WebPhoneBuddyStore> _logger;
    private List<WebPhoneBuddyRecord> _items;

    public WebPhoneBuddyStore(IHostEnvironment env, ILogger<WebPhoneBuddyStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "webphone-buddies.json");
        _items = Load();
    }

    public IReadOnlyList<WebPhoneBuddyDto> GetList(string? quickSearch = null)
    {
        lock (_gate)
        {
            IEnumerable<WebPhoneBuddyRecord> q = _items;
            if (!string.IsNullOrWhiteSpace(quickSearch))
            {
                var s = quickSearch.Trim();
                q = q.Where(b =>
                    (b.DisplayName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (b.ExtensionNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (b.Email?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (b.MobileNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return q.OrderBy(b => b.DisplayName).Select(ToDto).ToList();
        }
    }

    public WebPhoneBuddyDto? GetOne(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        lock (_gate)
        {
            var b = _items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            return b == null ? null : ToDto(b);
        }
    }

    public WebPhoneBuddyDto Add(WebPhoneBuddyUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ArgumentException("DisplayName is required.");

        lock (_gate)
        {
            var record = FromRequest(request, Guid.NewGuid().ToString("N"));
            _items.Add(record);
            PersistUnlocked();
            return ToDto(record);
        }
    }

    public WebPhoneBuddyDto Update(WebPhoneBuddyUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new ArgumentException("Id is required.");

        lock (_gate)
        {
            var idx = _items.FindIndex(x => string.Equals(x.Id, request.Id, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                throw new InvalidOperationException($"Buddy '{request.Id}' not found.");
            var record = FromRequest(request, _items[idx].Id);
            _items[idx] = record;
            PersistUnlocked();
            return ToDto(record);
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        lock (_gate)
        {
            var n = _items.RemoveAll(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (n > 0)
                PersistUnlocked();
            return n > 0;
        }
    }

    private List<WebPhoneBuddyRecord> Load()
    {
        if (!File.Exists(_path))
            return new List<WebPhoneBuddyRecord>();
        try
        {
            var doc = JsonSerializer.Deserialize<WebPhoneBuddiesDocument>(File.ReadAllText(_path), JsonOptions);
            return doc?.Buddies ?? new List<WebPhoneBuddyRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load webphone-buddies");
            return new List<WebPhoneBuddyRecord>();
        }
    }

    private void PersistUnlocked()
    {
        var json = JsonSerializer.Serialize(new WebPhoneBuddiesDocument { Buddies = _items.ToList() }, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    private static WebPhoneBuddyRecord FromRequest(WebPhoneBuddyUpsertRequest r, string id) => new()
    {
        Id = id,
        Type = string.IsNullOrWhiteSpace(r.Type) ? "extension" : r.Type.Trim(),
        DisplayName = r.DisplayName.Trim(),
        ExtensionNumber = NullIfWhiteSpace(r.ExtensionNumber),
        Description = NullIfWhiteSpace(r.Description),
        MobileNumber = NullIfWhiteSpace(r.MobileNumber),
        Email = NullIfWhiteSpace(r.Email),
        ContactNumber1 = NullIfWhiteSpace(r.ContactNumber1),
        ContactNumber2 = NullIfWhiteSpace(r.ContactNumber2),
        Subscribe = r.Subscribe ?? false,
        SubscribeUser = NullIfWhiteSpace(r.SubscribeUser),
        EnableDuringDnd = r.EnableDuringDnd ?? false,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static WebPhoneBuddyDto ToDto(WebPhoneBuddyRecord b) => new()
    {
        Id = b.Id,
        Type = b.Type,
        DisplayName = b.DisplayName,
        ExtensionNumber = b.ExtensionNumber,
        Description = b.Description,
        MobileNumber = b.MobileNumber,
        Email = b.Email,
        ContactNumber1 = b.ContactNumber1,
        ContactNumber2 = b.ContactNumber2,
        Subscribe = b.Subscribe,
        SubscribeUser = b.SubscribeUser,
        EnableDuringDnd = b.EnableDuringDnd,
        UpdatedAtUtc = b.UpdatedAtUtc
    };

    private static string? NullIfWhiteSpace(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
