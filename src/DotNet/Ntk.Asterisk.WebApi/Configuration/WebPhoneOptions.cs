namespace Ntk.Asterisk.WebApi.Configuration;

/// <summary>Feature flags for the hosted softphone (env-invariant defaults; overlays may override).</summary>
public sealed class WebPhoneOptions
{
    public const string SectionName = "WebPhone";

    /// <summary>Header accepted for shared-secret gate on GetSipConfig (and extension admin writes).</summary>
    public const string ApiKeyHeaderName = "X-WebPhone-Api-Key";

    /// <summary>Alternate header (same semantics as <see cref="ApiKeyHeaderName"/>).</summary>
    public const string ApiKeyHeaderAlias = "X-Api-Key";

    public bool EnableTransfer { get; set; } = true;
    public bool EnableConference { get; set; } = true;
    public bool EnableRecordAll { get; set; }
    public bool EnableVideo { get; set; } = true;
    public bool EnablePresence { get; set; } = true;
    public bool EnableMwi { get; set; } = true;
    public string DefaultHintContext { get; set; } = "from-internal";
    public string DefaultMailboxContext { get; set; } = "default";

    /// <summary>
    /// When true and <see cref="ApiKey"/> is non-empty, GetSipConfig requires a matching API-key header.
    /// Development default: false (open). Production: set true + ApiKey in overlay.
    /// </summary>
    public bool RequireApiKey { get; set; }

    /// <summary>
    /// Shared secret for softphone provision. Empty + RequireApiKey=false → open (local/dev).
    /// Never log this value.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Gate is active only when both RequireApiKey and a non-empty ApiKey are set.</summary>
    public bool IsApiKeyGateActive =>
        RequireApiKey && !string.IsNullOrWhiteSpace(ApiKey);
}
