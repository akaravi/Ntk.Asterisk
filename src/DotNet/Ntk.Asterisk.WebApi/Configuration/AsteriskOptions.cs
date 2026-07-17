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
    public int DefaultTimeoutMs { get; set; } = 30000;
    public string? DefaultCallerId { get; set; }
    public bool KeepAlive { get; set; } = true;
    public int PingIntervalMs { get; set; } = 10000;
    public bool AutoConnectOnStartup { get; set; } = true;

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
