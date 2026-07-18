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
    /// <summary>Active (default enabled) server id — additive for multi-server clients.</summary>
    public string? ServerId { get; init; }
    public string? ServerName { get; init; }
    /// <summary>AMI username (not secret) — additive for connection list.</summary>
    public string? Username { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsDefault { get; init; }
    /// <summary>
    /// True when this row is a persistent live endpoint (connected, ops primary, or previously opened).
    /// False only when never opened as live (legacy clients treated probe rows as false).
    /// </summary>
    public bool IsLiveSession { get; init; }
}

public sealed class ConnectionServerRequest
{
    /// <summary>Optional server id; null/blank → active default (ops) server.</summary>
    public string? ServerId { get; set; }
}

public sealed class PeerDto
{
    public string Id { get; init; } = string.Empty;
    public string Tech { get; init; } = string.Empty;
    /// <summary>Qualify / registration status, or live call label when in a call (Ringing / In Use).</summary>
    public string Status { get; init; } = string.Empty;
    public string? Ip { get; init; }
    public string? Channel { get; init; }
    public bool IsTrunk { get; init; }
    /// <summary>UTC of last meaningful activity (register / call state change) — additive.</summary>
    public DateTimeOffset? LastActivityUtc { get; init; }
    /// <summary>Idle / Ringing / InUse / Busy / Unavailable — additive live call overlay.</summary>
    public string? CallState { get; init; }
    /// <summary>Active channel age seconds when in a call — additive.</summary>
    public int? CallDurationSeconds { get; init; }
    /// <summary>Caller on the matched live channel — additive.</summary>
    public string? CallerId { get; init; }
    /// <summary>True when peer has an active/ringing channel — additive.</summary>
    public bool InCall { get; init; }
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
    /// <summary>Channel age in seconds from AMI Status — additive.</summary>
    public int? DurationSeconds { get; init; }
    /// <summary>When this snapshot was taken (live channel) — additive.</summary>
    public DateTimeOffset? LastActivityUtc { get; init; }
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

/// <summary>
/// Supervisor ExtenSpy (Voip.AsteriskChanSpyPro modes via AMI Originate).
/// Modes: listen | quiet | whisper | privateWhisper | barge | dtmf
/// </summary>
public sealed class ChanSpyRequest
{
    /// <summary>Extension that will be dialed to become the spy channel (supervisor).</summary>
    public string SupervisorExtension { get; set; } = string.Empty;
    /// <summary>Target extension whose active call is spied (ExtenSpy arg).</summary>
    public string TargetExtension { get; set; } = string.Empty;
    /// <summary>listen | quiet | whisper | privateWhisper | barge | dtmf</summary>
    public string Mode { get; set; } = "listen";
    /// <summary>Optional originate timeout seconds (default from settings).</summary>
    public int? TimeoutSec { get; set; }
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
    /// <summary>Basename e.g. ntk-{yyyyMMdd}-{HHmmss}-from-{from}-to-{to}.wav — download when RecordingAvailable.</summary>
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
    /// <summary>Active server meta — additive for multi-server clients.</summary>
    public string? ServerId { get; init; }
    public string? ServerName { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsDefault { get; init; } = true;
    public string? QueueHideList { get; init; }
    public string? QueueShowList { get; init; }
    public string? QueueRenameMap { get; init; }
    public string? CallFileStagingDirectory { get; init; }
    public string? CallFileOutgoingDirectory { get; init; }
}

/// <summary>List/detail DTO for multi-server Admin management (secret never returned).</summary>
public sealed class AsteriskServerDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsDefault { get; init; }
    public bool AmiConfigured { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? Username { get; init; }
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
    public string? SipWebsocketUrl { get; init; }
    public string? SipWebsocketHost { get; init; }
    public string? SipDomain { get; init; }
    public string? WebSocketPath { get; init; }
    public int? WebSocketPort { get; init; }
    public bool SipUseTls { get; init; }
    public string? StunServersJson { get; init; }
    public string? QueueHideList { get; init; }
    public string? QueueShowList { get; init; }
    public string? QueueRenameMap { get; init; }
    public string? CallFileStagingDirectory { get; init; }
    public string? CallFileOutgoingDirectory { get; init; }
}

public sealed class AsteriskServerAddRequest
{
    public string? Name { get; set; }
    public bool? IsEnabled { get; set; } = true;
    public bool? IsDefault { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Secret { get; set; }
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
    public string? SipWebsocketUrl { get; set; }
    public string? SipWebsocketHost { get; set; }
    public string? SipDomain { get; set; }
    public string? WebSocketPath { get; set; }
    public int? WebSocketPort { get; set; }
    public bool? SipUseTls { get; set; }
    public string? StunServersJson { get; set; }
    public string? QueueHideList { get; set; }
    public string? QueueShowList { get; set; }
    public string? QueueRenameMap { get; set; }
    public string? CallFileStagingDirectory { get; set; }
    public string? CallFileOutgoingDirectory { get; set; }
    public bool? ReconnectAfterSave { get; set; } = true;
}

public sealed class AsteriskServerUpdateRequest
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDefault { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
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
    public string? SipWebsocketUrl { get; set; }
    public string? SipWebsocketHost { get; set; }
    public string? SipDomain { get; set; }
    public string? WebSocketPath { get; set; }
    public int? WebSocketPort { get; set; }
    public bool? SipUseTls { get; set; }
    public string? StunServersJson { get; set; }
    public string? QueueHideList { get; set; }
    public string? QueueShowList { get; set; }
    public string? QueueRenameMap { get; set; }
    public string? CallFileStagingDirectory { get; set; }
    public string? CallFileOutgoingDirectory { get; set; }
    public bool? ReconnectAfterSave { get; set; } = true;
}

public sealed class AsteriskServerIdRequest
{
    public string Id { get; set; } = string.Empty;
    public bool? ReconnectAfterSave { get; set; } = true;
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

public sealed class CallFileAddRequest
{
    /// <summary>e.g. PJSIP/1001 or Local/0912…@from-internal</summary>
    public string Channel { get; set; } = string.Empty;
    public string? CallerId { get; set; }
    public int? WaitTimeSec { get; set; }
    public int? MaxRetries { get; set; }
    public int? RetryTimeSec { get; set; }
    /// <summary>yes | no</summary>
    public string Archive { get; set; } = "yes";
    public string? Application { get; set; }
    public string? Data { get; set; }
    public string? Context { get; set; }
    public string? Extension { get; set; }
    public string? Priority { get; set; }
    public Dictionary<string, string>? SetVars { get; set; }
}

public sealed class CallFileDto
{
    public string FileName { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string? OutgoingPath { get; init; }
    public string Mode { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class AsteriskSiteSettingsUpdateRequest
{
    /// <summary>Optional target server; null → active default enabled server.</summary>
    public string? ServerId { get; set; }
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
    public string? QueueHideList { get; set; }
    public string? QueueShowList { get; set; }
    public string? QueueRenameMap { get; set; }
    public string? CallFileStagingDirectory { get; set; }
    public string? CallFileOutgoingDirectory { get; set; }
    public bool? ReconnectAfterSave { get; set; } = true;
}
