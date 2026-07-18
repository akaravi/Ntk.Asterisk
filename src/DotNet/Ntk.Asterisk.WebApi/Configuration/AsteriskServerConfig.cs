namespace Ntk.Asterisk.WebApi.Configuration;

/// <summary>
/// One AMI/Asterisk endpoint managed by Admin. Multiple servers may exist;
/// only the default enabled server drives the live AMI session.
/// </summary>
public sealed class AsteriskServerConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }

    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Secret { get; set; }
    public string ChannelTech { get; set; } = "PJSIP";
    public string? DefaultTrunk { get; set; }
    public string? TrunkPeerFilter { get; set; }
    public string OriginateVia { get; set; } = "LocalContext";
    public string OriginateContext { get; set; } = "from-internal";
    public string MusicOnHoldClass { get; set; } = "default";
    public int DefaultTimeoutMs { get; set; } = 30000;
    public string? DefaultCallerId { get; set; }
    public bool KeepAlive { get; set; } = true;
    public int PingIntervalMs { get; set; } = 10000;
    public bool AutoConnectOnStartup { get; set; } = true;
    public bool RecordingEnabled { get; set; } = true;
    public string? RecordingLocalDirectory { get; set; }
    public string? RecordingHttpBaseUrl { get; set; }
    public string? RecordingAsteriskDirectory { get; set; }
    public string RecordingFormat { get; set; } = "wav";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is > 0
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Secret);

    public AsteriskOptions ToOptions() => new()
    {
        Host = Host,
        Port = Port,
        Username = Username,
        Secret = Secret,
        ChannelTech = ChannelTech,
        DefaultTrunk = DefaultTrunk,
        TrunkPeerFilter = TrunkPeerFilter,
        OriginateVia = OriginateVia,
        OriginateContext = OriginateContext,
        MusicOnHoldClass = MusicOnHoldClass,
        DefaultTimeoutMs = DefaultTimeoutMs,
        DefaultCallerId = DefaultCallerId,
        KeepAlive = KeepAlive,
        PingIntervalMs = PingIntervalMs,
        AutoConnectOnStartup = AutoConnectOnStartup,
        RecordingEnabled = RecordingEnabled,
        RecordingLocalDirectory = RecordingLocalDirectory,
        RecordingHttpBaseUrl = RecordingHttpBaseUrl,
        RecordingAsteriskDirectory = RecordingAsteriskDirectory,
        RecordingFormat = RecordingFormat
    };

    public static AsteriskServerConfig FromOptions(
        AsteriskOptions opt,
        string? id = null,
        string? name = null,
        bool isEnabled = true,
        bool isDefault = true) => new()
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim(),
        Name = string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim(),
        IsEnabled = isEnabled,
        IsDefault = isDefault,
        Host = opt.Host,
        Port = opt.Port,
        Username = opt.Username,
        Secret = opt.Secret,
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
        RecordingFormat = opt.RecordingFormat
    };
}

public sealed class AsteriskServersDocument
{
    public int Version { get; set; } = 1;
    public List<AsteriskServerConfig> Servers { get; set; } = new();
}
