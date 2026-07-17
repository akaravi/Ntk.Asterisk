namespace Ntk.Asterisk.WebApi.Contracts;

public sealed class ConnectionStatusDto
{
    public bool Connected { get; init; }
    public bool Configured { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset? ConnectedAtUtc { get; init; }
    /// <summary>ISO-8601 alias for clients expecting ConnectedSinceUtc.</summary>
    public string? ConnectedSinceUtc => ConnectedAtUtc?.ToString("O");
    public double? UptimeSeconds { get; init; }
    public string? AsteriskVersion { get; init; }
    public string ChannelTech { get; init; } = "PJSIP";
    public string? DefaultTrunk { get; init; }
    public bool AmiHostConfigured { get; init; }
    public bool AmiPortConfigured { get; init; }
    public bool AmiUserConfigured { get; init; }
    public bool AmiSecretConfigured { get; init; }
    public bool TrunkPeerFilterConfigured { get; init; }
}

public sealed class PeerDto
{
    public string Id { get; init; } = string.Empty;
    public string Tech { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Ip { get; init; }
    public string? Channel { get; init; }
    public bool IsTrunk { get; init; }
}

public sealed class ChannelDto
{
    public string Channel { get; init; } = string.Empty;
    public string? UniqueId { get; init; }
    public string? State { get; init; }
    public string? CallerId { get; init; }
    public string? Exten { get; init; }
    public string? Context { get; init; }
    public string? Application { get; init; }
}

public sealed class HangupRequest
{
    public string Channel { get; set; } = string.Empty;
}

public sealed class BridgeRequest
{
    public string Channel1 { get; set; } = string.Empty;
    public string Channel2 { get; set; } = string.Empty;
    public string Tone { get; set; } = "no";
}

public sealed class CallJobAddRequest
{
    public string Type { get; set; } = string.Empty;
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Mobile1 { get; set; }
    public string? Mobile2 { get; set; }
    public int? TimeoutSec { get; set; }
    public string? CallerId { get; set; }
    public string? Trunk { get; set; }
}

public sealed class CallJobDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    /// <summary>Legacy alias for UserPanel clients expecting status.</summary>
    public string Status => State;
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Mobile1 { get; init; }
    public string? Mobile2 { get; init; }
    public string? ActionId { get; init; }
    public string? Channel { get; init; }
    public string? UniqueId { get; init; }
    public string? ErrorMessage { get; init; }
    /// <summary>Outcome reason for success or failure (additive; prefer over ErrorMessage in UI).</summary>
    public string? ResultReason { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    /// <summary>Alias for CreatedAtUtc — job/call request time.</summary>
    public DateTimeOffset CallTimeUtc => CreatedAtUtc;
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public int? DurationSeconds { get; init; }
    public bool IsCommandJob { get; init; }
    /// <summary>MixMonitor was started for this job.</summary>
    public bool HasRecording { get; init; }
    /// <summary>Basename e.g. ntk-{id}.wav — download when RecordingAvailable.</summary>
    public string? RecordingFileName { get; init; }
    /// <summary>True when WebApi can serve the audio file now.</summary>
    public bool RecordingAvailable { get; init; }
}

public sealed class ConfigVisibilityDto
{
    public bool AmiConfigured { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    /// <summary>Editable username for admin form (not a secret).</summary>
    public string? Username { get; init; }
    /// <summary>Legacy masked hint for older clients.</summary>
    public string? UsernameConfigured { get; init; }
    public bool SecretConfigured { get; init; }
    public string ChannelTech { get; init; } = "PJSIP";
    public string? DefaultTrunk { get; init; }
    public string? TrunkPeerFilter { get; init; }
    public string OriginateVia { get; init; } = "LocalContext";
    public string OriginateContext { get; init; } = "from-internal";
    public string MusicOnHoldClass { get; init; } = "default";
    public int DefaultTimeoutMs { get; init; }
    public string? DefaultCallerId { get; init; }
    public bool KeepAlive { get; init; } = true;
    public int PingIntervalMs { get; init; } = 10000;
    public bool AutoConnectOnStartup { get; init; } = true;
    public bool RecordingEnabled { get; init; } = true;
    public string? RecordingLocalDirectory { get; init; }
    public string? RecordingHttpBaseUrl { get; init; }
    public string? RecordingAsteriskDirectory { get; init; }
    public string RecordingFormat { get; init; } = "wav";
    public bool Persisted { get; init; }
    public string Note { get; init; } = "Managed via Admin Settings. Secrets never returned.";
}

public sealed class LiveEventDto
{
    public string Id { get; init; } = string.Empty;
    public DateTimeOffset AtUtc { get; init; }
    /// <summary>ami | app | system</summary>
    public string Source { get; init; } = "ami";
    /// <summary>AMI event type, logger category, or system topic.</summary>
    public string Category { get; init; } = string.Empty;
    /// <summary>Event | Trace | Debug | Information | Warning | Error | Critical</summary>
    public string Level { get; init; } = "Event";
    public string Message { get; init; } = string.Empty;
    public string? Channel { get; init; }
    public string? UniqueId { get; init; }
    public string? Privilege { get; init; }
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}

public sealed class AsteriskSiteSettingsUpdateRequest
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    /// <summary>Null/blank keeps existing secret.</summary>
    public string? Secret { get; set; }
    public bool ClearSecret { get; set; }
    public string? ChannelTech { get; set; }
    public string? DefaultTrunk { get; set; }
    public string? TrunkPeerFilter { get; set; }
    public string? OriginateVia { get; set; }
    public string? OriginateContext { get; set; }
    public string? MusicOnHoldClass { get; set; }
    public int? DefaultTimeoutMs { get; set; }
    public string? DefaultCallerId { get; set; }
    public bool? KeepAlive { get; set; }
    public int? PingIntervalMs { get; set; }
    public bool? AutoConnectOnStartup { get; set; }
    public bool? RecordingEnabled { get; set; }
    public string? RecordingLocalDirectory { get; set; }
    public string? RecordingHttpBaseUrl { get; set; }
    public string? RecordingAsteriskDirectory { get; set; }
    public string? RecordingFormat { get; set; }
    public bool? ReconnectAfterSave { get; set; } = true;
}
