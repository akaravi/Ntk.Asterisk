using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IAsteriskSettingsService
{
    AsteriskOptions GetEffective();
    ConfigVisibilityDto GetSiteSettings();
    ConfigVisibilityDto UpdateSiteSettings(AsteriskSiteSettingsUpdateRequest request);
}

/// <summary>
/// Runtime AMI settings: seeded from appsettings overlays, persisted under App_Data for admin edits.
/// Secret is never returned on GET; blank secret on Update keeps the previous value.
/// </summary>
public sealed class AsteriskSettingsService : IAsteriskSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHostEnvironment _env;
    private readonly IOptionsMonitor<AsteriskOptions> _bootstrap;
    private readonly ILogger<AsteriskSettingsService> _logger;
    private readonly object _gate = new();
    private AsteriskOptions _current;
    private readonly string _filePath;

    public AsteriskSettingsService(
        IHostEnvironment env,
        IOptionsMonitor<AsteriskOptions> bootstrap,
        ILogger<AsteriskSettingsService> logger)
    {
        _env = env;
        _bootstrap = bootstrap;
        _logger = logger;
        _filePath = Path.Combine(_env.ContentRootPath, "App_Data", "asterisk-settings.json");
        _current = LoadOrSeed();
    }

    public AsteriskOptions GetEffective()
    {
        lock (_gate)
            return Clone(_current);
    }

    public ConfigVisibilityDto GetSiteSettings()
    {
        var opt = GetEffective();
        return ToDto(opt, File.Exists(_filePath));
    }

    public ConfigVisibilityDto UpdateSiteSettings(AsteriskSiteSettingsUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        AsteriskOptions next;
        lock (_gate)
        {
            next = Clone(_current);
            if (request.Host != null)
                next.Host = NullIfWhiteSpace(request.Host);
            if (request.Port.HasValue)
                next.Port = request.Port.Value <= 0 ? null : request.Port.Value;
            if (request.Username != null)
                next.Username = NullIfWhiteSpace(request.Username);

            if (request.ClearSecret)
                next.Secret = null;
            else if (!string.IsNullOrWhiteSpace(request.Secret))
                next.Secret = request.Secret.Trim();

            if (request.ChannelTech != null)
                next.ChannelTech = string.IsNullOrWhiteSpace(request.ChannelTech)
                    ? "PJSIP"
                    : request.ChannelTech.Trim();
            if (request.DefaultTrunk != null)
                next.DefaultTrunk = NullIfWhiteSpace(request.DefaultTrunk);
            if (request.TrunkPeerFilter != null)
                next.TrunkPeerFilter = NullIfWhiteSpace(request.TrunkPeerFilter);
            if (request.DefaultTimeoutMs.HasValue && request.DefaultTimeoutMs.Value > 0)
                next.DefaultTimeoutMs = request.DefaultTimeoutMs.Value;
            if (request.DefaultCallerId != null)
                next.DefaultCallerId = NullIfWhiteSpace(request.DefaultCallerId);
            if (request.KeepAlive.HasValue)
                next.KeepAlive = request.KeepAlive.Value;
            if (request.PingIntervalMs.HasValue && request.PingIntervalMs.Value > 0)
                next.PingIntervalMs = request.PingIntervalMs.Value;
            if (request.AutoConnectOnStartup.HasValue)
                next.AutoConnectOnStartup = request.AutoConnectOnStartup.Value;

            PersistUnlocked(next);
            _current = next;
        }

        _logger.LogInformation(
            "AMI site settings updated by admin. Host={Host} Port={Port} UserSet={UserSet} SecretSet={SecretSet}",
            next.Host,
            next.Port,
            !string.IsNullOrWhiteSpace(next.Username),
            !string.IsNullOrWhiteSpace(next.Secret));

        return ToDto(next, persisted: true);
    }

    private AsteriskOptions LoadOrSeed()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var persisted = JsonSerializer.Deserialize<AsteriskOptions>(json, JsonOptions);
                if (persisted != null)
                {
                    _logger.LogInformation("Loaded AMI settings from {Path}", _filePath);
                    return Normalize(persisted);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load AMI settings from {Path}; using appsettings seed", _filePath);
        }

        return Normalize(Clone(_bootstrap.CurrentValue));
    }

    private void PersistUnlocked(AsteriskOptions options)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(Normalize(options), JsonOptions);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true);
    }

    private static ConfigVisibilityDto ToDto(AsteriskOptions opt, bool persisted) => new()
    {
        AmiConfigured = opt.IsConfigured,
        Host = opt.Host,
        Port = opt.Port,
        Username = opt.Username,
        UsernameConfigured = string.IsNullOrWhiteSpace(opt.Username) ? null : Mask(opt.Username),
        SecretConfigured = !string.IsNullOrWhiteSpace(opt.Secret),
        ChannelTech = opt.ChannelTech,
        DefaultTrunk = opt.DefaultTrunk,
        TrunkPeerFilter = opt.TrunkPeerFilter,
        DefaultTimeoutMs = opt.DefaultTimeoutMs,
        DefaultCallerId = opt.DefaultCallerId,
        KeepAlive = opt.KeepAlive,
        PingIntervalMs = opt.PingIntervalMs,
        AutoConnectOnStartup = opt.AutoConnectOnStartup,
        Persisted = persisted,
        Note = "Managed via Admin Settings. Secret is never returned; leave blank to keep existing."
    };

    private static AsteriskOptions Clone(AsteriskOptions src) => new()
    {
        Host = src.Host,
        Port = src.Port,
        Username = src.Username,
        Secret = src.Secret,
        ChannelTech = src.ChannelTech,
        DefaultTrunk = src.DefaultTrunk,
        TrunkPeerFilter = src.TrunkPeerFilter,
        DefaultTimeoutMs = src.DefaultTimeoutMs,
        DefaultCallerId = src.DefaultCallerId,
        KeepAlive = src.KeepAlive,
        PingIntervalMs = src.PingIntervalMs,
        AutoConnectOnStartup = src.AutoConnectOnStartup
    };

    private static AsteriskOptions Normalize(AsteriskOptions opt)
    {
        opt.ChannelTech = string.IsNullOrWhiteSpace(opt.ChannelTech) ? "PJSIP" : opt.ChannelTech.Trim();
        if (opt.DefaultTimeoutMs <= 0) opt.DefaultTimeoutMs = 30000;
        if (opt.PingIntervalMs <= 0) opt.PingIntervalMs = 10000;
        return opt;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Mask(string value)
    {
        if (value.Length <= 2) return "**";
        return value[0] + new string('*', Math.Min(6, value.Length - 1));
    }
}
