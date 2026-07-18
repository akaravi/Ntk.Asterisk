using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhoneExtensionStore
{
    IReadOnlyList<WebPhoneExtensionDto> GetList();
    WebPhoneExtensionRecord? FindByUsername(string sipUsername);
    WebPhoneExtensionRecord? FindById(string id);
    WebPhoneExtensionDto Upsert(WebPhoneExtensionUpsertRequest request);
    bool Delete(string id);
}

public sealed class WebPhoneExtensionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SipUsername { get; set; } = string.Empty;
    public string SipPassword { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string? ServerId { get; set; }
    public bool IsEnabled { get; set; } = true;
}

internal sealed class WebPhoneExtensionsDocument
{
    public int Version { get; set; } = 1;
    public List<WebPhoneExtensionRecord> Extensions { get; set; } = new();
}

/// <summary>File-backed SIP extension credentials for softphone provision (no Issabel MySQL).</summary>
public sealed class WebPhoneExtensionStore : IWebPhoneExtensionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<WebPhoneExtensionStore> _logger;
    private List<WebPhoneExtensionRecord> _items;

    public WebPhoneExtensionStore(IHostEnvironment env, ILogger<WebPhoneExtensionStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "webphone-extensions.json");
        _items = Load();
    }

    public IReadOnlyList<WebPhoneExtensionDto> GetList()
    {
        lock (_gate)
            return _items.Select(ToDto).ToList();
    }

    public WebPhoneExtensionRecord? FindByUsername(string sipUsername)
    {
        if (string.IsNullOrWhiteSpace(sipUsername))
            return null;
        lock (_gate)
            return _items.FirstOrDefault(x =>
                x.IsEnabled
                && string.Equals(x.SipUsername, sipUsername.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public WebPhoneExtensionRecord? FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        lock (_gate)
            return _items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public WebPhoneExtensionDto Upsert(WebPhoneExtensionUpsertRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SipUsername))
            throw new ArgumentException("SipUsername is required.");

        lock (_gate)
        {
            WebPhoneExtensionRecord record;
            if (!string.IsNullOrWhiteSpace(request.Id))
            {
                record = _items.FirstOrDefault(x => string.Equals(x.Id, request.Id, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Extension '{request.Id}' not found.");
            }
            else
            {
                record = _items.FirstOrDefault(x =>
                    string.Equals(x.SipUsername, request.SipUsername.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? new WebPhoneExtensionRecord { Id = Guid.NewGuid().ToString("N") };
                if (!_items.Contains(record))
                    _items.Add(record);
            }

            record.SipUsername = request.SipUsername.Trim();
            if (!string.IsNullOrWhiteSpace(request.SipPassword))
                record.SipPassword = request.SipPassword.Trim();
            if (request.ProfileName != null)
                record.ProfileName = request.ProfileName.Trim();
            if (string.IsNullOrWhiteSpace(record.ProfileName))
                record.ProfileName = record.SipUsername;
            if (request.ServerId != null)
                record.ServerId = string.IsNullOrWhiteSpace(request.ServerId) ? null : request.ServerId.Trim();
            if (request.IsEnabled.HasValue)
                record.IsEnabled = request.IsEnabled.Value;

            PersistUnlocked();
            _logger.LogInformation("WebPhone extension upserted username={Username} id={Id}", record.SipUsername, record.Id);
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

    private List<WebPhoneExtensionRecord> Load()
    {
        if (!File.Exists(_path))
            TrySeedFromExample();

        if (!File.Exists(_path))
        {
            _logger.LogInformation(
                "No webphone-extensions at {Path}; copy App_Data/webphone-extensions.json.example → webphone-extensions.json for GetSipConfig smoke",
                _path);
            return new List<WebPhoneExtensionRecord>();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<WebPhoneExtensionsDocument>(json, JsonOptions);
            return doc?.Extensions ?? new List<WebPhoneExtensionRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load webphone-extensions; starting empty");
            return new List<WebPhoneExtensionRecord>();
        }
    }

    /// <summary>
    /// Local-only seed: copies committed example (placeholder password) into gitignored live file.
    /// Replace sipPassword with a real Asterisk secret before production register.
    /// </summary>
    private void TrySeedFromExample()
    {
        var example = _path + ".example";
        if (!File.Exists(example))
            return;

        try
        {
            File.Copy(example, _path, overwrite: false);
            _logger.LogInformation(
                "Seeded webphone-extensions.json from example at {Path} (placeholder secret; not for production)",
                _path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not seed webphone-extensions from example");
        }
    }

    private void PersistUnlocked()
    {
        var doc = new WebPhoneExtensionsDocument { Extensions = _items.ToList() };
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    private static WebPhoneExtensionDto ToDto(WebPhoneExtensionRecord r) => new()
    {
        Id = r.Id,
        SipUsername = r.SipUsername,
        ProfileName = r.ProfileName,
        ServerId = r.ServerId,
        IsEnabled = r.IsEnabled,
        SecretConfigured = !string.IsNullOrWhiteSpace(r.SipPassword)
    };
}
