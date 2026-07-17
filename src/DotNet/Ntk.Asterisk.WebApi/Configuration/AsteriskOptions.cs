namespace Ntk.Asterisk.WebApi.Configuration;

public sealed class AsteriskOptions
{
    public const string SectionName = "Asterisk";

    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Secret { get; set; }
    public string ChannelTech { get; set; } = "PJSIP";
    public string? DefaultTrunk { get; set; }
    public string? TrunkPeerFilter { get; set; }
    /// <summary>
    /// LocalContext (default): FreePBX-safe Local/number@context — uses outbound routes.
    /// DirectTech: dial PJSIP/number@trunk or SIP/trunk/number without dialplan routes.
    /// </summary>
    public string OriginateVia { get; set; } = "LocalContext";
    /// <summary>Dialplan context for Local channels (FreePBX: from-internal).</summary>
    public string OriginateContext { get; set; } = "from-internal";
    /// <summary>Asterisk MOH class played to leg1 until leg2 answers (FreePBX: default).</summary>
    public string MusicOnHoldClass { get; set; } = "default";
    public int DefaultTimeoutMs { get; set; } = 30000;
    public string? DefaultCallerId { get; set; }
    public bool KeepAlive { get; set; } = true;
    public int PingIntervalMs { get; set; } = 10000;
    public bool AutoConnectOnStartup { get; set; } = true;

    /// <summary>Start AMI MixMonitor when a call is bridged.</summary>
    public bool RecordingEnabled { get; set; } = true;

    /// <summary>
    /// Directory on the WebApi host where Asterisk monitor files are visible
    /// (UNC mount of /var/spool/asterisk/monitor, rsync target, etc.).
    /// </summary>
    public string? RecordingLocalDirectory { get; set; }

    /// <summary>
    /// Optional HTTP base that serves monitor files (e.g. https://pbx/monitor/).
    /// Used to pull into App_Data/recordings for download.
    /// </summary>
    public string? RecordingHttpBaseUrl { get; set; }

    /// <summary>
    /// Absolute directory on the Asterisk host for MixMonitor File= path.
    /// Empty → basename only (Asterisk default monitor spool).
    /// FreePBX typical: /var/spool/asterisk/monitor
    /// </summary>
    public string? RecordingAsteriskDirectory { get; set; }

    public string RecordingFormat { get; set; } = "wav";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is > 0
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Secret);
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
