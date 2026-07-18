using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhoneQosStore
{
    WebPhoneQosDto Add(WebPhoneQosAddRequest request);
    IReadOnlyList<WebPhoneQosDto> GetList(int take = 100);
}

internal sealed class WebPhoneQosRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? CallId { get; set; }
    public string? BuddyId { get; set; }
    public double? Mos { get; set; }
    public double? PacketLossPct { get; set; }
    public double? JitterMs { get; set; }
    public double? RttMs { get; set; }
    public string? RawJson { get; set; }
    public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class WebPhoneQosDocument
{
    public int Version { get; set; } = 1;
    public List<WebPhoneQosRecord> Items { get; set; } = new();
}

public sealed class WebPhoneQosStore : IWebPhoneQosStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<WebPhoneQosStore> _logger;
    private List<WebPhoneQosRecord> _items;

    public WebPhoneQosStore(IHostEnvironment env, ILogger<WebPhoneQosStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "webphone-qos.json");
        _items = Load();
    }

    public WebPhoneQosDto Add(WebPhoneQosAddRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var record = new WebPhoneQosRecord
            {
                CallId = NullIfWhiteSpace(request.CallId),
                BuddyId = NullIfWhiteSpace(request.BuddyId),
                Mos = request.Mos,
                PacketLossPct = request.PacketLossPct,
                JitterMs = request.JitterMs,
                RttMs = request.RttMs,
                RawJson = Truncate(request.RawJson, 8000),
                AtUtc = DateTimeOffset.UtcNow
            };
            _items.Add(record);
            if (_items.Count > 2000)
                _items = _items.OrderByDescending(x => x.AtUtc).Take(1500).ToList();
            PersistUnlocked();
            return ToDto(record);
        }
    }

    public IReadOnlyList<WebPhoneQosDto> GetList(int take = 100)
    {
        var n = take <= 0 ? 100 : Math.Min(take, 500);
        lock (_gate)
            return _items.OrderByDescending(x => x.AtUtc).Take(n).Select(ToDto).ToList();
    }

    private List<WebPhoneQosRecord> Load()
    {
        if (!File.Exists(_path))
            return new List<WebPhoneQosRecord>();
        try
        {
            var doc = JsonSerializer.Deserialize<WebPhoneQosDocument>(File.ReadAllText(_path), JsonOptions);
            return doc?.Items ?? new List<WebPhoneQosRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load webphone-qos");
            return new List<WebPhoneQosRecord>();
        }
    }

    private void PersistUnlocked()
    {
        var json = JsonSerializer.Serialize(new WebPhoneQosDocument { Items = _items.ToList() }, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    private static WebPhoneQosDto ToDto(WebPhoneQosRecord r) => new()
    {
        Id = r.Id,
        CallId = r.CallId,
        BuddyId = r.BuddyId,
        Mos = r.Mos,
        PacketLossPct = r.PacketLossPct,
        JitterMs = r.JitterMs,
        RttMs = r.RttMs,
        RawJson = r.RawJson,
        AtUtc = r.AtUtc
    };

    private static string? NullIfWhiteSpace(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? null : (s.Length <= max ? s : s[..max]);
}
