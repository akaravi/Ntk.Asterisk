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

    /// <summary>Full WebSocket SIP URL (ws/wss). When set, preferred over host+port+path.</summary>
    public string? SipWebsocketUrl { get; set; }
    /// <summary>Host for SIP WebSocket when SipWebsocketUrl is empty (often same as AMI Host).</summary>
    public string? SipWebsocketHost { get; set; }
    public string? SipDomain { get; set; }
    public string? WebSocketPath { get; set; } = "/ws";
    public int? WebSocketPort { get; set; } = 8089;
    public bool SipUseTls { get; set; }
    /// <summary>JSON array of STUN URLs, e.g. ["stun:stun.l.google.com:19302"].</summary>
    public string? StunServersJson { get; set; }

    /// <summary>Comma/newline AMI queue names to hide (QPanel hide).</summary>
    public string? QueueHideList { get; set; }
    /// <summary>Comma/newline allow-list (QPanel show). When set, hide list is ignored.</summary>
    public string? QueueShowList { get; set; }
    /// <summary>Lines realName=Display Name (QPanel rename).</summary>
    public string? QueueRenameMap { get; set; }

    /// <summary>Staging for .call (same volume as outgoing).</summary>
    public string? CallFileStagingDirectory { get; set; }
    /// <summary>UNC/local path mapped to Asterisk outgoing spool.</summary>
    public string? CallFileOutgoingDirectory { get; set; }

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
        RecordingFormat = RecordingFormat,
        SipWebsocketUrl = SipWebsocketUrl,
        SipWebsocketHost = SipWebsocketHost,
        SipDomain = SipDomain,
        WebSocketPath = WebSocketPath,
        WebSocketPort = WebSocketPort,
        SipUseTls = SipUseTls,
        StunServersJson = StunServersJson,
        QueueHideList = QueueHideList,
        QueueShowList = QueueShowList,
        QueueRenameMap = QueueRenameMap,
        CallFileStagingDirectory = CallFileStagingDirectory,
        CallFileOutgoingDirectory = CallFileOutgoingDirectory,
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
}

public sealed class AsteriskServersDocument
{
    public int Version { get; set; } = 1;
    public List<AsteriskServerConfig> Servers { get; set; } = new();
}
