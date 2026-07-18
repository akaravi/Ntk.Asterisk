using System.Text.Json.Serialization;

namespace Ntk.Asterisk.WebApi.Contracts;

/// <summary>VOIZ-compatible SIP provision payload (secrets only on this endpoint).</summary>
public sealed class SipConfigDto
{
    [JsonPropertyName("SipUsername")]
    public string SipUsername { get; init; } = string.Empty;

    [JsonPropertyName("SipPassword")]
    public string SipPassword { get; init; } = string.Empty;

    [JsonPropertyName("profileName")]
    public string ProfileName { get; init; } = string.Empty;

    [JsonPropertyName("serverIP")]
    public string ServerIp { get; init; } = string.Empty;

    [JsonPropertyName("wssUrl")]
    public string? WssUrl { get; init; }

    [JsonPropertyName("WebSocketPort")]
    public string WebSocketPort { get; init; } = "8089";

    [JsonPropertyName("ServerPath")]
    public string ServerPath { get; init; } = "/ws";

    [JsonPropertyName("SipDomain")]
    public string SipDomain { get; init; } = string.Empty;

    [JsonPropertyName("SipUseTls")]
    public bool SipUseTls { get; init; }

    [JsonPropertyName("StunServersJson")]
    public string? StunServersJson { get; init; }

    public string? ServerId { get; init; }
    public WebPhoneOptionsDto? Features { get; init; }
}

public sealed class SipConfigRequest
{
    /// <summary>Extension / auth user. When empty, first enabled store entry or query is used.</summary>
    public string? SipUsername { get; set; }
    public string? ServerId { get; set; }
}

public sealed class WebPhoneExtensionDto
{
    public string Id { get; init; } = string.Empty;
    public string SipUsername { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public string? ServerId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool SecretConfigured { get; init; }
}

public sealed class WebPhoneExtensionUpsertRequest
{
    public string? Id { get; set; }
    public string SipUsername { get; set; } = string.Empty;
    public string? SipPassword { get; set; }
    public string? ProfileName { get; set; }
    public string? ServerId { get; set; }
    public bool? IsEnabled { get; set; } = true;
}

public sealed class WebPhoneBuddyDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = "extension";
    public string DisplayName { get; init; } = string.Empty;
    public string? ExtensionNumber { get; init; }
    public string? Description { get; init; }
    public string? MobileNumber { get; init; }
    public string? Email { get; init; }
    public string? ContactNumber1 { get; init; }
    public string? ContactNumber2 { get; init; }
    public bool Subscribe { get; init; }
    public string? SubscribeUser { get; init; }
    public bool EnableDuringDnd { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class WebPhoneBuddyUpsertRequest
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ExtensionNumber { get; set; }
    public string? Description { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public string? ContactNumber1 { get; set; }
    public string? ContactNumber2 { get; set; }
    public bool? Subscribe { get; set; }
    public string? SubscribeUser { get; set; }
    public bool? EnableDuringDnd { get; set; }
}

public sealed class WebPhoneBuddyIdRequest
{
    public string Id { get; set; } = string.Empty;
}

public sealed class WebPhoneCdrDto
{
    public string Id { get; init; } = string.Empty;
    public string? BuddyId { get; init; }
    public string? Direction { get; init; }
    public string? WithNumber { get; init; }
    public string? DisplayName { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public int? DurationSeconds { get; init; }
    public string? Disposition { get; init; }
    public string? RecordingId { get; init; }
    public string? Notes { get; init; }
}

public sealed class WebPhoneCdrAddRequest
{
    public string? BuddyId { get; set; }
    public string? Direction { get; set; }
    public string? WithNumber { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Disposition { get; set; }
    public string? RecordingId { get; set; }
    public string? Notes { get; set; }
}

public sealed class WebPhonePresenceQueryRequest
{
    public string? Extension { get; set; }
    public string? Context { get; set; }
}

public sealed class WebPhoneMwiQueryRequest
{
    public string? Mailbox { get; set; }
}

public sealed class WebPhonePresenceDto
{
    public string Extension { get; init; } = string.Empty;
    public string? Context { get; init; }
    public string State { get; init; } = "Unknown";
    public int? StatusCode { get; init; }
    public string? Device { get; init; }
    public DateTimeOffset AtUtc { get; init; }
}

public sealed class WebPhoneMwiDto
{
    public string Mailbox { get; init; } = string.Empty;
    public int NewMessages { get; init; }
    public int OldMessages { get; init; }
    public bool Waiting { get; init; }
    public DateTimeOffset AtUtc { get; init; }
}

public sealed class WebPhoneRecordingMetaDto
{
    public string Id { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public long SizeBytes { get; init; }
    public string? BuddyId { get; init; }
    public string? CdrId { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class WebPhoneQosDto
{
    public string Id { get; init; } = string.Empty;
    public string? CallId { get; init; }
    public string? BuddyId { get; init; }
    public double? Mos { get; init; }
    public double? PacketLossPct { get; init; }
    public double? JitterMs { get; init; }
    public double? RttMs { get; init; }
    public string? RawJson { get; init; }
    public DateTimeOffset AtUtc { get; init; }
}

public sealed class WebPhoneQosAddRequest
{
    public string? CallId { get; set; }
    public string? BuddyId { get; set; }
    public double? Mos { get; set; }
    public double? PacketLossPct { get; set; }
    public double? JitterMs { get; set; }
    public double? RttMs { get; set; }
    public string? RawJson { get; set; }
}

public sealed class WebPhoneOptionsDto
{
    public bool EnableTransfer { get; init; }
    public bool EnableConference { get; init; }
    public bool EnableRecordAll { get; init; }
    public bool EnableVideo { get; init; }
    public bool EnablePresence { get; init; }
    public bool EnableMwi { get; init; }
    /// <summary>True when server requires X-WebPhone-Api-Key for GetSipConfig (never exposes the key).</summary>
    public bool RequireApiKey { get; init; }
}
