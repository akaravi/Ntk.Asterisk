using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Contracts;

namespace Ntk.Asterisk.WebApi.Services;

public interface IAsteriskSettingsService
{
    /// <summary>Options for the active (default enabled) server — drives AMI session.</summary>
    AsteriskOptions GetEffective();

    /// <summary>Active server record, or null when none enabled/default.</summary>
    AsteriskServerConfig? GetActiveServer();

    /// <summary>Enabled servers with secrets — for live multi-AMI sessions.</summary>
    IReadOnlyList<AsteriskServerConfig> GetEnabledServerConfigs();

    IReadOnlyList<AsteriskServerDto> GetServerList();
    AsteriskServerDto? GetServer(string id);
    AsteriskServerDto AddServer(AsteriskServerAddRequest request);
    AsteriskServerDto UpdateServer(AsteriskServerUpdateRequest request);
    AsteriskServerDto SetServerEnabled(string id, bool enabled);
    AsteriskServerDto SetDefaultServer(string id);
    AsteriskServerDto DeleteServer(string id);

    ConfigVisibilityDto GetSiteSettings();
    ConfigVisibilityDto UpdateSiteSettings(AsteriskSiteSettingsUpdateRequest request);
}

/// <summary>
/// Multi-server AMI settings store. Seeded from appsettings / legacy single-file JSON,
/// persisted under App_Data/asterisk-servers.json. Secret never returned on GET;
/// blank secret on Update keeps the previous value.
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
    private readonly string _serversPath;
    private readonly string _legacyPath;
    private List<AsteriskServerConfig> _servers;

    public AsteriskSettingsService(
        IHostEnvironment env,
        IOptionsMonitor<AsteriskOptions> bootstrap,
        ILogger<AsteriskSettingsService> logger)
    {
        _env = env;
        _bootstrap = bootstrap;
        _logger = logger;
        var dataDir = Path.Combine(_env.ContentRootPath, "App_Data");
        _serversPath = Path.Combine(dataDir, "asterisk-servers.json");
        _legacyPath = Path.Combine(dataDir, "asterisk-settings.json");
        _servers = LoadOrSeed();
    }

    public AsteriskOptions GetEffective()
    {
        lock (_gate)
        {
            var active = ResolveActiveUnlocked();
            return active == null
                ? NormalizeOptions(new AsteriskOptions())
                : NormalizeOptions(active.ToOptions());
        }
    }

    public AsteriskServerConfig? GetActiveServer()
    {
        lock (_gate)
        {
            var active = ResolveActiveUnlocked();
            return active == null ? null : CloneServer(active);
        }
    }

    public IReadOnlyList<AsteriskServerConfig> GetEnabledServerConfigs()
    {
        lock (_gate)
            return _servers.Where(s => s.IsEnabled).Select(CloneServer).ToList();
    }

    public IReadOnlyList<AsteriskServerDto> GetServerList()
    {
        lock (_gate)
            return _servers.Select(ToServerDto).ToList();
    }

    public AsteriskServerDto? GetServer(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        lock (_gate)
        {
            var s = FindUnlocked(id);
            return s == null ? null : ToServerDto(s);
        }
    }

    public AsteriskServerDto AddServer(AsteriskServerAddRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        AsteriskServerConfig created;
        lock (_gate)
        {
            created = new AsteriskServerConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Server {_servers.Count + 1}" : request.Name.Trim(),
                IsEnabled = request.IsEnabled ?? true,
                IsDefault = false,
                Host = NullIfWhiteSpace(request.Host),
                Port = request.Port is > 0 ? request.Port : 5038,
                Username = NullIfWhiteSpace(request.Username),
                Secret = NullIfWhiteSpace(request.Secret),
                ChannelTech = string.IsNullOrWhiteSpace(request.ChannelTech) ? "PJSIP" : request.ChannelTech.Trim(),
                DefaultTrunk = NullIfWhiteSpace(request.DefaultTrunk),
                TrunkPeerFilter = NullIfWhiteSpace(request.TrunkPeerFilter),
                OriginateVia = string.IsNullOrWhiteSpace(request.OriginateVia) ? "LocalContext" : request.OriginateVia.Trim(),
                OriginateContext = string.IsNullOrWhiteSpace(request.OriginateContext) ? "from-internal" : request.OriginateContext.Trim(),
                MusicOnHoldClass = string.IsNullOrWhiteSpace(request.MusicOnHoldClass) ? "default" : request.MusicOnHoldClass.Trim(),
                DefaultTimeoutMs = request.DefaultTimeoutMs is > 0 ? request.DefaultTimeoutMs.Value : 30000,
                DefaultCallerId = NullIfWhiteSpace(request.DefaultCallerId),
                KeepAlive = request.KeepAlive ?? true,
                PingIntervalMs = request.PingIntervalMs is > 0 ? request.PingIntervalMs.Value : 10000,
                AutoConnectOnStartup = request.AutoConnectOnStartup ?? true,
                RecordingEnabled = request.RecordingEnabled ?? true,
                RecordingLocalDirectory = NullIfWhiteSpace(request.RecordingLocalDirectory),
                RecordingHttpBaseUrl = NullIfWhiteSpace(request.RecordingHttpBaseUrl),
                RecordingAsteriskDirectory = NullIfWhiteSpace(request.RecordingAsteriskDirectory)
                    ?? "/var/spool/asterisk/monitor",
                RecordingFormat = string.IsNullOrWhiteSpace(request.RecordingFormat) ? "wav" : request.RecordingFormat.Trim().Trim('.'),
                SipWebsocketUrl = NullIfWhiteSpace(request.SipWebsocketUrl),
                SipWebsocketHost = NullIfWhiteSpace(request.SipWebsocketHost),
                SipDomain = NullIfWhiteSpace(request.SipDomain),
                WebSocketPath = string.IsNullOrWhiteSpace(request.WebSocketPath) ? "/ws" : request.WebSocketPath.Trim(),
                WebSocketPort = request.WebSocketPort is > 0 ? request.WebSocketPort : 8089,
                SipUseTls = request.SipUseTls ?? false,
                StunServersJson = NullIfWhiteSpace(request.StunServersJson),
                QueueHideList = NullIfWhiteSpace(request.QueueHideList),
                QueueShowList = NullIfWhiteSpace(request.QueueShowList),
                QueueRenameMap = NullIfWhiteSpace(request.QueueRenameMap),
                CallFileStagingDirectory = NullIfWhiteSpace(request.CallFileStagingDirectory),
                CallFileOutgoingDirectory = NullIfWhiteSpace(request.CallFileOutgoingDirectory),
            };

            NormalizeServer(created);
            _servers.Add(created);

            var makeDefault = request.IsDefault == true || !_servers.Any(s => s.IsDefault && s.IsEnabled);
            if (makeDefault)
                SetDefaultUnlocked(created.Id);

            EnsureDefaultInvariantUnlocked();
            PersistUnlocked();
        }

        _logger.LogInformation("AMI server added Id={Id} Name={Name}", created.Id, created.Name);
        return ToServerDto(created);
    }

    public AsteriskServerDto UpdateServer(AsteriskServerUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new ArgumentException("Server Id is required.", nameof(request));

        AsteriskServerConfig target;
        lock (_gate)
        {
            target = FindUnlocked(request.Id)
                ?? throw new InvalidOperationException($"Server '{request.Id}' not found.");

            ApplyConnectionFields(
                target,
                request.Host,
                request.Port,
                request.Username,
                request.Secret,
                request.ClearSecret,
                request.ChannelTech,
                request.DefaultTrunk,
                request.TrunkPeerFilter,
                request.OriginateVia,
                request.OriginateContext,
                request.MusicOnHoldClass,
                request.DefaultTimeoutMs,
                request.DefaultCallerId,
                request.KeepAlive,
                request.PingIntervalMs,
                request.AutoConnectOnStartup,
                request.RecordingEnabled,
                request.RecordingLocalDirectory,
                request.RecordingHttpBaseUrl,
                request.RecordingAsteriskDirectory,
                request.RecordingFormat);

            ApplySipFields(
                target,
                request.SipWebsocketUrl,
                request.SipWebsocketHost,
                request.SipDomain,
                request.WebSocketPath,
                request.WebSocketPort,
                request.SipUseTls,
                request.StunServersJson);

            ApplyQueueDisplayFields(
                target,
                request.QueueHideList,
                request.QueueShowList,
                request.QueueRenameMap);

            ApplyCallFileFields(
                target,
                request.CallFileStagingDirectory,
                request.CallFileOutgoingDirectory);

            if (request.Name != null)
                target.Name = string.IsNullOrWhiteSpace(request.Name) ? target.Name : request.Name.Trim();

            if (request.IsEnabled.HasValue)
                target.IsEnabled = request.IsEnabled.Value;

            if (request.IsDefault == true)
                SetDefaultUnlocked(target.Id);

            NormalizeServer(target);
            EnsureDefaultInvariantUnlocked();
            PersistUnlocked();
        }

        _logger.LogInformation("AMI server updated Id={Id} Name={Name}", target.Id, target.Name);
        return ToServerDto(target);
    }

    public AsteriskServerDto SetServerEnabled(string id, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Server Id is required.", nameof(id));

        AsteriskServerConfig target;
        lock (_gate)
        {
            target = FindUnlocked(id)
                ?? throw new InvalidOperationException($"Server '{id}' not found.");
            target.IsEnabled = enabled;
            EnsureDefaultInvariantUnlocked();
            PersistUnlocked();
        }

        _logger.LogInformation("AMI server {State} Id={Id}", enabled ? "enabled" : "disabled", id);
        return ToServerDto(target);
    }

    public AsteriskServerDto SetDefaultServer(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Server Id is required.", nameof(id));

        AsteriskServerConfig target;
        lock (_gate)
        {
            target = FindUnlocked(id)
                ?? throw new InvalidOperationException($"Server '{id}' not found.");
            if (!target.IsEnabled)
                throw new InvalidOperationException("Cannot set a disabled server as default. Enable it first.");

            SetDefaultUnlocked(id);
            PersistUnlocked();
        }

        _logger.LogInformation("AMI default server set Id={Id} Name={Name}", target.Id, target.Name);
        return ToServerDto(target);
    }

    public AsteriskServerDto DeleteServer(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Server Id is required.", nameof(id));

        AsteriskServerConfig removed;
        lock (_gate)
        {
            removed = FindUnlocked(id)
                ?? throw new InvalidOperationException($"Server '{id}' not found.");
            if (_servers.Count <= 1)
                throw new InvalidOperationException("Cannot delete the last server. Add another server first.");

            _servers.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            EnsureDefaultInvariantUnlocked();
            PersistUnlocked();
        }

        _logger.LogInformation("AMI server deleted Id={Id}", id);
        return ToServerDto(removed);
    }

    public ConfigVisibilityDto GetSiteSettings()
    {
        lock (_gate)
        {
            var active = ResolveActiveUnlocked() ?? _servers.FirstOrDefault();
            if (active == null)
            {
                return new ConfigVisibilityDto
                {
                    AmiConfigured = false,
                    Persisted = File.Exists(_serversPath) || File.Exists(_legacyPath),
                    Note = "No AMI servers configured. Add a server in Admin Settings."
                };
            }

            return ToVisibilityDto(active, File.Exists(_serversPath) || File.Exists(_legacyPath));
        }
    }

    public ConfigVisibilityDto UpdateSiteSettings(AsteriskSiteSettingsUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        AsteriskServerConfig target;
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(request.ServerId))
            {
                target = FindUnlocked(request.ServerId)
                    ?? throw new InvalidOperationException($"Server '{request.ServerId}' not found.");
            }
            else
            {
                target = ResolveActiveUnlocked()
                    ?? _servers.FirstOrDefault()
                    ?? throw new InvalidOperationException("No AMI server configured. Add a server first.");
            }

            ApplyConnectionFields(
                target,
                request.Host,
                request.Port,
                request.Username,
                request.Secret,
                request.ClearSecret,
                request.ChannelTech,
                request.DefaultTrunk,
                request.TrunkPeerFilter,
                request.OriginateVia,
                request.OriginateContext,
                request.MusicOnHoldClass,
                request.DefaultTimeoutMs,
                request.DefaultCallerId,
                request.KeepAlive,
                request.PingIntervalMs,
                request.AutoConnectOnStartup,
                request.RecordingEnabled,
                request.RecordingLocalDirectory,
                request.RecordingHttpBaseUrl,
                request.RecordingAsteriskDirectory,
                request.RecordingFormat);

            ApplyQueueDisplayFields(
                target,
                request.QueueHideList,
                request.QueueShowList,
                request.QueueRenameMap);

            ApplyCallFileFields(
                target,
                request.CallFileStagingDirectory,
                request.CallFileOutgoingDirectory);

            NormalizeServer(target);
            EnsureDefaultInvariantUnlocked();
            PersistUnlocked();
        }

        _logger.LogInformation(
            "AMI site settings updated ServerId={ServerId} Host={Host} Port={Port}",
            target.Id,
            target.Host,
            target.Port);

        return ToVisibilityDto(target, persisted: true);
    }

    private List<AsteriskServerConfig> LoadOrSeed()
    {
        try
        {
            if (File.Exists(_serversPath))
            {
                var json = File.ReadAllText(_serversPath);
                var doc = JsonSerializer.Deserialize<AsteriskServersDocument>(json, JsonOptions);
                if (doc?.Servers is { Count: > 0 })
                {
                    foreach (var s in doc.Servers)
                        NormalizeServer(s);
                    EnsureDefaultInvariant(doc.Servers);
                    _logger.LogInformation("Loaded {Count} AMI server(s) from {Path}", doc.Servers.Count, _serversPath);
                    return doc.Servers;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load AMI servers from {Path}", _serversPath);
        }

        // Legacy single-server file → wrap as one default server
        try
        {
            if (File.Exists(_legacyPath))
            {
                var json = File.ReadAllText(_legacyPath);
                var persisted = JsonSerializer.Deserialize<AsteriskOptions>(json, JsonOptions);
                if (persisted != null)
                {
                    var wrapped = AsteriskServerConfig.FromOptions(persisted, name: "Default");
                    NormalizeServer(wrapped);
                    var list = new List<AsteriskServerConfig> { wrapped };
                    PersistList(list);
                    _logger.LogInformation("Migrated legacy AMI settings to multi-server store at {Path}", _serversPath);
                    return list;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate legacy AMI settings from {Path}", _legacyPath);
        }

        var seed = AsteriskServerConfig.FromOptions(
            NormalizeOptions(CloneOptions(_bootstrap.CurrentValue)),
            name: "Default");
        NormalizeServer(seed);
        return new List<AsteriskServerConfig> { seed };
    }

    private void PersistUnlocked() => PersistList(_servers);

    private void PersistList(List<AsteriskServerConfig> servers)
    {
        var dir = Path.GetDirectoryName(_serversPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var doc = new AsteriskServersDocument
        {
            Version = 1,
            Servers = servers.Select(CloneServer).ToList()
        };
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        var tmp = _serversPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _serversPath, overwrite: true);

        // Keep legacy file in sync with active server for older tooling
        var active = ResolveActiveFrom(servers) ?? servers.FirstOrDefault();
        if (active != null)
        {
            var legacyJson = JsonSerializer.Serialize(NormalizeOptions(active.ToOptions()), JsonOptions);
            var legacyTmp = _legacyPath + ".tmp";
            File.WriteAllText(legacyTmp, legacyJson);
            File.Move(legacyTmp, _legacyPath, overwrite: true);
        }
    }

    private AsteriskServerConfig? FindUnlocked(string id) =>
        _servers.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    private AsteriskServerConfig? ResolveActiveUnlocked() => ResolveActiveFrom(_servers);

    private static AsteriskServerConfig? ResolveActiveFrom(IReadOnlyList<AsteriskServerConfig> servers)
    {
        var preferred = servers.FirstOrDefault(s => s.IsDefault && s.IsEnabled);
        if (preferred != null)
            return preferred;
        return servers.FirstOrDefault(s => s.IsEnabled);
    }

    private void SetDefaultUnlocked(string id)
    {
        foreach (var s in _servers)
            s.IsDefault = string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureDefaultInvariantUnlocked() => EnsureDefaultInvariant(_servers);

    private static void EnsureDefaultInvariant(List<AsteriskServerConfig> servers)
    {
        if (servers.Count == 0)
            return;

        var defaults = servers.Where(s => s.IsDefault).ToList();
        if (defaults.Count > 1)
        {
            var keep = defaults.FirstOrDefault(s => s.IsEnabled) ?? defaults[0];
            foreach (var s in servers)
                s.IsDefault = ReferenceEquals(s, keep) || string.Equals(s.Id, keep.Id, StringComparison.OrdinalIgnoreCase);
        }
        else if (defaults.Count == 0)
        {
            var promote = servers.FirstOrDefault(s => s.IsEnabled) ?? servers[0];
            promote.IsDefault = true;
        }
        else if (defaults[0] is { IsEnabled: false })
        {
            var promote = servers.FirstOrDefault(s => s.IsEnabled);
            if (promote != null)
            {
                foreach (var s in servers)
                    s.IsDefault = string.Equals(s.Id, promote.Id, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static void ApplyConnectionFields(
        AsteriskServerConfig target,
        string? host,
        int? port,
        string? username,
        string? secret,
        bool clearSecret,
        string? channelTech,
        string? defaultTrunk,
        string? trunkPeerFilter,
        string? originateVia,
        string? originateContext,
        string? musicOnHoldClass,
        int? defaultTimeoutMs,
        string? defaultCallerId,
        bool? keepAlive,
        int? pingIntervalMs,
        bool? autoConnectOnStartup,
        bool? recordingEnabled,
        string? recordingLocalDirectory,
        string? recordingHttpBaseUrl,
        string? recordingAsteriskDirectory,
        string? recordingFormat)
    {
        if (host != null)
            target.Host = NullIfWhiteSpace(host);
        if (port.HasValue)
            target.Port = port.Value <= 0 ? null : port.Value;
        if (username != null)
            target.Username = NullIfWhiteSpace(username);

        if (clearSecret)
            target.Secret = null;
        else if (!string.IsNullOrWhiteSpace(secret))
            target.Secret = secret.Trim();

        if (channelTech != null)
            target.ChannelTech = string.IsNullOrWhiteSpace(channelTech) ? "PJSIP" : channelTech.Trim();
        if (defaultTrunk != null)
            target.DefaultTrunk = NullIfWhiteSpace(defaultTrunk);
        if (trunkPeerFilter != null)
            target.TrunkPeerFilter = NullIfWhiteSpace(trunkPeerFilter);
        if (originateVia != null)
            target.OriginateVia = string.IsNullOrWhiteSpace(originateVia) ? "LocalContext" : originateVia.Trim();
        if (originateContext != null)
            target.OriginateContext = string.IsNullOrWhiteSpace(originateContext) ? "from-internal" : originateContext.Trim();
        if (musicOnHoldClass != null)
            target.MusicOnHoldClass = string.IsNullOrWhiteSpace(musicOnHoldClass) ? "default" : musicOnHoldClass.Trim();
        if (defaultTimeoutMs.HasValue && defaultTimeoutMs.Value > 0)
            target.DefaultTimeoutMs = defaultTimeoutMs.Value;
        if (defaultCallerId != null)
            target.DefaultCallerId = NullIfWhiteSpace(defaultCallerId);
        if (keepAlive.HasValue)
            target.KeepAlive = keepAlive.Value;
        if (pingIntervalMs.HasValue && pingIntervalMs.Value > 0)
            target.PingIntervalMs = pingIntervalMs.Value;
        if (autoConnectOnStartup.HasValue)
            target.AutoConnectOnStartup = autoConnectOnStartup.Value;
        if (recordingEnabled.HasValue)
            target.RecordingEnabled = recordingEnabled.Value;
        if (recordingLocalDirectory != null)
            target.RecordingLocalDirectory = NullIfWhiteSpace(recordingLocalDirectory);
        if (recordingHttpBaseUrl != null)
            target.RecordingHttpBaseUrl = NullIfWhiteSpace(recordingHttpBaseUrl);
        if (recordingAsteriskDirectory != null)
            target.RecordingAsteriskDirectory = NullIfWhiteSpace(recordingAsteriskDirectory);
        if (recordingFormat != null)
            target.RecordingFormat = string.IsNullOrWhiteSpace(recordingFormat)
                ? "wav"
                : recordingFormat.Trim().Trim('.');
    }

    private static void ApplySipFields(
        AsteriskServerConfig target,
        string? sipWebsocketUrl,
        string? sipWebsocketHost,
        string? sipDomain,
        string? webSocketPath,
        int? webSocketPort,
        bool? sipUseTls,
        string? stunServersJson)
    {
        if (sipWebsocketUrl != null)
            target.SipWebsocketUrl = NullIfWhiteSpace(sipWebsocketUrl);
        if (sipWebsocketHost != null)
            target.SipWebsocketHost = NullIfWhiteSpace(sipWebsocketHost);
        if (sipDomain != null)
            target.SipDomain = NullIfWhiteSpace(sipDomain);
        if (webSocketPath != null)
            target.WebSocketPath = string.IsNullOrWhiteSpace(webSocketPath) ? "/ws" : webSocketPath.Trim();
        if (webSocketPort.HasValue)
            target.WebSocketPort = webSocketPort.Value <= 0 ? 8089 : webSocketPort.Value;
        if (sipUseTls.HasValue)
            target.SipUseTls = sipUseTls.Value;
        if (stunServersJson != null)
            target.StunServersJson = NullIfWhiteSpace(stunServersJson);
    }

    private static void ApplyQueueDisplayFields(
        AsteriskServerConfig target,
        string? queueHideList,
        string? queueShowList,
        string? queueRenameMap)
    {
        if (queueHideList != null)
            target.QueueHideList = NullIfWhiteSpace(queueHideList);
        if (queueShowList != null)
            target.QueueShowList = NullIfWhiteSpace(queueShowList);
        if (queueRenameMap != null)
            target.QueueRenameMap = NullIfWhiteSpace(queueRenameMap);
    }

    private static void ApplyCallFileFields(
        AsteriskServerConfig target,
        string? callFileStagingDirectory,
        string? callFileOutgoingDirectory)
    {
        if (callFileStagingDirectory != null)
            target.CallFileStagingDirectory = NullIfWhiteSpace(callFileStagingDirectory);
        if (callFileOutgoingDirectory != null)
            target.CallFileOutgoingDirectory = NullIfWhiteSpace(callFileOutgoingDirectory);
    }

    private static ConfigVisibilityDto ToVisibilityDto(AsteriskServerConfig opt, bool persisted) => new()
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
        OriginateVia = opt.OriginateVia,
        OriginateContext = opt.OriginateContext,
        MusicOnHoldClass = opt.MusicOnHoldClass,
        DefaultTimeoutMs = opt.DefaultTimeoutMs,
        DefaultCallerId = opt.DefaultCallerId,
        KeepAlive = opt.KeepAlive,
        PingIntervalMs = opt.PingIntervalMs,
        AutoConnectOnStartup = opt.AutoConnectOnStartup,
        RecordingEnabled = opt.RecordingEnabled,
        RecordingLocalDirectory = opt.RecordingLocalDirectory,
        RecordingHttpBaseUrl = opt.RecordingHttpBaseUrl,
        RecordingAsteriskDirectory = opt.RecordingAsteriskDirectory,
        RecordingFormat = opt.RecordingFormat,
        Persisted = persisted,
        ServerId = opt.Id,
        ServerName = opt.Name,
        IsEnabled = opt.IsEnabled,
        IsDefault = opt.IsDefault,
        QueueHideList = opt.QueueHideList,
        QueueShowList = opt.QueueShowList,
        QueueRenameMap = opt.QueueRenameMap,
        CallFileStagingDirectory = opt.CallFileStagingDirectory,
        CallFileOutgoingDirectory = opt.CallFileOutgoingDirectory,
        Note = "Managed via Admin Settings. Secret is never returned; leave blank to keep existing."
    };

    private static AsteriskServerDto ToServerDto(AsteriskServerConfig opt) => new()
    {
        Id = opt.Id,
        Name = opt.Name,
        IsEnabled = opt.IsEnabled,
        IsDefault = opt.IsDefault,
        AmiConfigured = opt.IsConfigured,
        Host = opt.Host,
        Port = opt.Port,
        Username = opt.Username,
        SecretConfigured = !string.IsNullOrWhiteSpace(opt.Secret),
        ChannelTech = opt.ChannelTech,
        DefaultTrunk = opt.DefaultTrunk,
        TrunkPeerFilter = opt.TrunkPeerFilter,
        OriginateVia = opt.OriginateVia,
        OriginateContext = opt.OriginateContext,
        MusicOnHoldClass = opt.MusicOnHoldClass,
        DefaultTimeoutMs = opt.DefaultTimeoutMs,
        DefaultCallerId = opt.DefaultCallerId,
        KeepAlive = opt.KeepAlive,
        PingIntervalMs = opt.PingIntervalMs,
        AutoConnectOnStartup = opt.AutoConnectOnStartup,
        RecordingEnabled = opt.RecordingEnabled,
        RecordingLocalDirectory = opt.RecordingLocalDirectory,
        RecordingHttpBaseUrl = opt.RecordingHttpBaseUrl,
        RecordingAsteriskDirectory = opt.RecordingAsteriskDirectory,
        RecordingFormat = opt.RecordingFormat,
        SipWebsocketUrl = opt.SipWebsocketUrl,
        SipWebsocketHost = opt.SipWebsocketHost,
        SipDomain = opt.SipDomain,
        WebSocketPath = opt.WebSocketPath,
        WebSocketPort = opt.WebSocketPort,
        SipUseTls = opt.SipUseTls,
        StunServersJson = opt.StunServersJson,
        QueueHideList = opt.QueueHideList,
        QueueShowList = opt.QueueShowList,
        QueueRenameMap = opt.QueueRenameMap,
        CallFileStagingDirectory = opt.CallFileStagingDirectory,
        CallFileOutgoingDirectory = opt.CallFileOutgoingDirectory,
    };

    private static AsteriskServerConfig CloneServer(AsteriskServerConfig src) => new()
    {
        Id = src.Id,
        Name = src.Name,
        IsEnabled = src.IsEnabled,
        IsDefault = src.IsDefault,
        Host = src.Host,
        Port = src.Port,
        Username = src.Username,
        Secret = src.Secret,
        ChannelTech = src.ChannelTech,
        DefaultTrunk = src.DefaultTrunk,
        TrunkPeerFilter = src.TrunkPeerFilter,
        OriginateVia = src.OriginateVia,
        OriginateContext = src.OriginateContext,
        MusicOnHoldClass = src.MusicOnHoldClass,
        DefaultTimeoutMs = src.DefaultTimeoutMs,
        DefaultCallerId = src.DefaultCallerId,
        KeepAlive = src.KeepAlive,
        PingIntervalMs = src.PingIntervalMs,
        AutoConnectOnStartup = src.AutoConnectOnStartup,
        RecordingEnabled = src.RecordingEnabled,
        RecordingLocalDirectory = src.RecordingLocalDirectory,
        RecordingHttpBaseUrl = src.RecordingHttpBaseUrl,
        RecordingAsteriskDirectory = src.RecordingAsteriskDirectory,
        RecordingFormat = src.RecordingFormat,
        SipWebsocketUrl = src.SipWebsocketUrl,
        SipWebsocketHost = src.SipWebsocketHost,
        SipDomain = src.SipDomain,
        WebSocketPath = src.WebSocketPath,
        WebSocketPort = src.WebSocketPort,
        SipUseTls = src.SipUseTls,
        StunServersJson = src.StunServersJson,
        QueueHideList = src.QueueHideList,
        QueueShowList = src.QueueShowList,
        QueueRenameMap = src.QueueRenameMap,
        CallFileStagingDirectory = src.CallFileStagingDirectory,
        CallFileOutgoingDirectory = src.CallFileOutgoingDirectory,
    };

    private static AsteriskOptions CloneOptions(AsteriskOptions src) => new()
    {
        Host = src.Host,
        Port = src.Port,
        Username = src.Username,
        Secret = src.Secret,
        ChannelTech = src.ChannelTech,
        DefaultTrunk = src.DefaultTrunk,
        TrunkPeerFilter = src.TrunkPeerFilter,
        OriginateVia = src.OriginateVia,
        OriginateContext = src.OriginateContext,
        MusicOnHoldClass = src.MusicOnHoldClass,
        DefaultTimeoutMs = src.DefaultTimeoutMs,
        DefaultCallerId = src.DefaultCallerId,
        KeepAlive = src.KeepAlive,
        PingIntervalMs = src.PingIntervalMs,
        AutoConnectOnStartup = src.AutoConnectOnStartup,
        RecordingEnabled = src.RecordingEnabled,
        RecordingLocalDirectory = src.RecordingLocalDirectory,
        RecordingHttpBaseUrl = src.RecordingHttpBaseUrl,
        RecordingAsteriskDirectory = src.RecordingAsteriskDirectory,
        RecordingFormat = src.RecordingFormat,
        SipWebsocketUrl = src.SipWebsocketUrl,
        SipWebsocketHost = src.SipWebsocketHost,
        SipDomain = src.SipDomain,
        WebSocketPath = src.WebSocketPath,
        WebSocketPort = src.WebSocketPort,
        SipUseTls = src.SipUseTls,
        StunServersJson = src.StunServersJson,
        QueueHideList = src.QueueHideList,
        QueueShowList = src.QueueShowList,
        QueueRenameMap = src.QueueRenameMap,
        CallFileStagingDirectory = src.CallFileStagingDirectory,
        CallFileOutgoingDirectory = src.CallFileOutgoingDirectory,
    };

    private static void NormalizeServer(AsteriskServerConfig opt)
    {
        if (string.IsNullOrWhiteSpace(opt.Id))
            opt.Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(opt.Name))
            opt.Name = "Default";
        opt.ChannelTech = string.IsNullOrWhiteSpace(opt.ChannelTech) ? "PJSIP" : opt.ChannelTech.Trim();
        if (opt.DefaultTimeoutMs <= 0) opt.DefaultTimeoutMs = 30000;
        if (opt.PingIntervalMs <= 0) opt.PingIntervalMs = 10000;
        opt.OriginateVia = string.IsNullOrWhiteSpace(opt.OriginateVia) ? "LocalContext" : opt.OriginateVia.Trim();
        opt.OriginateContext = string.IsNullOrWhiteSpace(opt.OriginateContext)
            ? "from-internal"
            : opt.OriginateContext.Trim();
        opt.MusicOnHoldClass = string.IsNullOrWhiteSpace(opt.MusicOnHoldClass)
            ? "default"
            : opt.MusicOnHoldClass.Trim();
        opt.RecordingFormat = string.IsNullOrWhiteSpace(opt.RecordingFormat)
            ? "wav"
            : opt.RecordingFormat.Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(opt.RecordingAsteriskDirectory))
            opt.RecordingAsteriskDirectory = "/var/spool/asterisk/monitor";
        if (string.IsNullOrWhiteSpace(opt.WebSocketPath))
            opt.WebSocketPath = "/ws";
        if (opt.WebSocketPort is null or <= 0)
            opt.WebSocketPort = 8089;
    }

    private static AsteriskOptions NormalizeOptions(AsteriskOptions opt)
    {
        opt.ChannelTech = string.IsNullOrWhiteSpace(opt.ChannelTech) ? "PJSIP" : opt.ChannelTech.Trim();
        if (opt.DefaultTimeoutMs <= 0) opt.DefaultTimeoutMs = 30000;
        if (opt.PingIntervalMs <= 0) opt.PingIntervalMs = 10000;
        opt.OriginateVia = string.IsNullOrWhiteSpace(opt.OriginateVia) ? "LocalContext" : opt.OriginateVia.Trim();
        opt.OriginateContext = string.IsNullOrWhiteSpace(opt.OriginateContext)
            ? "from-internal"
            : opt.OriginateContext.Trim();
        opt.MusicOnHoldClass = string.IsNullOrWhiteSpace(opt.MusicOnHoldClass)
            ? "default"
            : opt.MusicOnHoldClass.Trim();
        opt.RecordingFormat = string.IsNullOrWhiteSpace(opt.RecordingFormat)
            ? "wav"
            : opt.RecordingFormat.Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(opt.RecordingAsteriskDirectory))
            opt.RecordingAsteriskDirectory = "/var/spool/asterisk/monitor";
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
