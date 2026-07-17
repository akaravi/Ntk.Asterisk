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
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public bool IsCommandJob { get; init; }
}

public sealed class ConfigVisibilityDto
{
    public bool AmiConfigured { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? UsernameConfigured { get; init; }
    public bool SecretConfigured { get; init; }
    public string ChannelTech { get; init; } = "PJSIP";
    public string? DefaultTrunk { get; init; }
    public string? TrunkPeerFilter { get; init; }
    public int DefaultTimeoutMs { get; init; }
    public string? DefaultCallerId { get; init; }
    public string Note { get; init; } = "Restart host after overlay change. Secrets never returned.";
}
