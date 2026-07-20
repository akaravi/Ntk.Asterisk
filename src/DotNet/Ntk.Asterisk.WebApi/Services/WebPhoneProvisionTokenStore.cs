using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IWebPhoneProvisionTokenStore
{
    IReadOnlyList<WebPhoneProvisionTokenDto> GetList();
    WebPhoneProvisionTokenCreatedDto Create(WebPhoneProvisionTokenCreateRequest request);
    bool Delete(string id);
    WebPhoneProvisionTokenRecord? FindByToken(string token);
    void TouchLastUsed(string id);
}

public sealed class WebPhoneProvisionTokenRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Token { get; set; } = string.Empty;
    public string ExtensionId { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

internal sealed class WebPhoneProvisionTokensDocument
{
    public int Version { get; set; } = 1;
    public List<WebPhoneProvisionTokenRecord> Tokens { get; set; } = new();
}

/// <summary>Admin-issued opaque tokens for one-shot WebPhone bootstrap (SIP + buddies + options).</summary>
public sealed class WebPhoneProvisionTokenStore : IWebPhoneProvisionTokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly ILogger<WebPhoneProvisionTokenStore> _logger;
    private List<WebPhoneProvisionTokenRecord> _items;

    public WebPhoneProvisionTokenStore(IHostEnvironment env, ILogger<WebPhoneProvisionTokenStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "webphone-provision-tokens.json");
        _items = Load();
    }

    public IReadOnlyList<WebPhoneProvisionTokenDto> GetList()
    {
        lock (_gate)
            return _items.Select(ToDto).OrderByDescending(x => x.CreatedAtUtc).ToList();
    }

    public WebPhoneProvisionTokenCreatedDto Create(WebPhoneProvisionTokenCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ExtensionId))
            throw new ArgumentException("ExtensionId is required.");

        lock (_gate)
        {
            var record = new WebPhoneProvisionTokenRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Token = GenerateToken(),
                ExtensionId = request.ExtensionId.Trim(),
                Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim(),
                IsEnabled = request.IsEnabled ?? true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = request.ExpiresAtUtc
            };
            _items.Add(record);
            PersistUnlocked();
            _logger.LogInformation(
                "WebPhone provision token created id={Id} extensionId={ExtensionId}",
                record.Id,
                record.ExtensionId);
            return new WebPhoneProvisionTokenCreatedDto
            {
                Id = record.Id,
                Token = record.Token,
                ExtensionId = record.ExtensionId,
                Label = record.Label,
                IsEnabled = record.IsEnabled,
                CreatedAtUtc = record.CreatedAtUtc,
                ExpiresAtUtc = record.ExpiresAtUtc
            };
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

    public WebPhoneProvisionTokenRecord? FindByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        lock (_gate)
        {
            var record = _items.FirstOrDefault(x =>
                x.IsEnabled && string.Equals(x.Token, token.Trim(), StringComparison.Ordinal));
            if (record is null)
                return null;
            if (record.ExpiresAtUtc.HasValue && record.ExpiresAtUtc.Value < DateTimeOffset.UtcNow)
                return null;
            return record;
        }
    }

    public void TouchLastUsed(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        lock (_gate)
        {
            var record = _items.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (record is null)
                return;
            record.LastUsedAtUtc = DateTimeOffset.UtcNow;
            PersistUnlocked();
        }
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private List<WebPhoneProvisionTokenRecord> Load()
    {
        if (!File.Exists(_path))
            return new List<WebPhoneProvisionTokenRecord>();

        try
        {
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<WebPhoneProvisionTokensDocument>(json, JsonOptions);
            return doc?.Tokens ?? new List<WebPhoneProvisionTokenRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load webphone-provision-tokens; starting empty");
            return new List<WebPhoneProvisionTokenRecord>();
        }
    }

    private void PersistUnlocked()
    {
        var doc = new WebPhoneProvisionTokensDocument { Tokens = _items.ToList() };
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }

    private static WebPhoneProvisionTokenDto ToDto(WebPhoneProvisionTokenRecord r) => new()
    {
        Id = r.Id,
        ExtensionId = r.ExtensionId,
        Label = r.Label,
        IsEnabled = r.IsEnabled,
        CreatedAtUtc = r.CreatedAtUtc,
        LastUsedAtUtc = r.LastUsedAtUtc,
        ExpiresAtUtc = r.ExpiresAtUtc
    };
}
