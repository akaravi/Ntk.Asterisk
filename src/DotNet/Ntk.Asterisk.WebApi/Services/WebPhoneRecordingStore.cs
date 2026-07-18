using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhoneRecordingStore
{
    Task<WebPhoneRecordingMetaDto> SaveAsync(
        Stream content,
        string? fileName,
        string? contentType,
        string? buddyId,
        string? cdrId,
        string? notes,
        CancellationToken cancellationToken);
    IReadOnlyList<WebPhoneRecordingMetaDto> GetList();
    string? ResolveFilePath(string id);
}

internal sealed class WebPhoneRecordingMetaRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? BuddyId { get; set; }
    public string? CdrId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class WebPhoneRecordingsDocument
{
    public int Version { get; set; } = 1;
    public List<WebPhoneRecordingMetaRecord> Items { get; set; } = new();
}

public sealed class WebPhoneRecordingStore : IWebPhoneRecordingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _dir;
    private readonly string _metaPath;
    private readonly ILogger<WebPhoneRecordingStore> _logger;
    private List<WebPhoneRecordingMetaRecord> _items;

    public WebPhoneRecordingStore(IHostEnvironment env, ILogger<WebPhoneRecordingStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        _dir = Path.Combine(dataDir, "webphone-recordings");
        Directory.CreateDirectory(_dir);
        _metaPath = Path.Combine(dataDir, "webphone-recordings.json");
        _items = Load();
    }

    public async Task<WebPhoneRecordingMetaDto> SaveAsync(
        Stream content,
        string? fileName,
        string? contentType,
        string? buddyId,
        string? cdrId,
        string? notes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var id = Guid.NewGuid().ToString("N");
        var safeName = SanitizeFileName(fileName) ?? $"recording-{id}.webm";
        var stored = $"{id}-{safeName}";
        var path = Path.Combine(_dir, stored);

        await using (var fs = File.Create(path))
            await content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);

        var info = new FileInfo(path);
        var record = new WebPhoneRecordingMetaRecord
        {
            Id = id,
            FileName = safeName,
            StoredName = stored,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            SizeBytes = info.Length,
            BuddyId = NullIfWhiteSpace(buddyId),
            CdrId = NullIfWhiteSpace(cdrId),
            Notes = NullIfWhiteSpace(notes),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        lock (_gate)
        {
            _items.Add(record);
            PersistUnlocked();
        }

        _logger.LogInformation("WebPhone recording saved id={Id} bytes={Bytes}", id, record.SizeBytes);
        return ToDto(record);
    }

    public IReadOnlyList<WebPhoneRecordingMetaDto> GetList()
    {
        lock (_gate)
            return _items.OrderByDescending(x => x.CreatedAtUtc).Select(ToDto).ToList();
    }

    public string? ResolveFilePath(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        lock (_gate)
        {
            var item = _items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return null;
            var path = Path.Combine(_dir, item.StoredName);
            return File.Exists(path) ? path : null;
        }
    }

    private List<WebPhoneRecordingMetaRecord> Load()
    {
        if (!File.Exists(_metaPath))
            return new List<WebPhoneRecordingMetaRecord>();
        try
        {
            var doc = JsonSerializer.Deserialize<WebPhoneRecordingsDocument>(File.ReadAllText(_metaPath), JsonOptions);
            return doc?.Items ?? new List<WebPhoneRecordingMetaRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load webphone-recordings meta");
            return new List<WebPhoneRecordingMetaRecord>();
        }
    }

    private void PersistUnlocked()
    {
        var json = JsonSerializer.Serialize(new WebPhoneRecordingsDocument { Items = _items.ToList() }, JsonOptions);
        var tmp = _metaPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _metaPath, overwrite: true);
    }

    private static WebPhoneRecordingMetaDto ToDto(WebPhoneRecordingMetaRecord r) => new()
    {
        Id = r.Id,
        FileName = r.FileName,
        ContentType = r.ContentType,
        SizeBytes = r.SizeBytes,
        BuddyId = r.BuddyId,
        CdrId = r.CdrId,
        Notes = r.Notes,
        CreatedAtUtc = r.CreatedAtUtc
    };

    private static string? SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var baseName = Path.GetFileName(name.Trim());
        foreach (var c in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '_');
        return string.IsNullOrWhiteSpace(baseName) ? null : baseName;
    }

    private static string? NullIfWhiteSpace(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
