namespace Ntk.Asterisk.WebApi.Contracts;

/// <summary>Live Asterisk queue snapshot (AMI QueueStatus) — Queue Panel parity.</summary>
public sealed class QueueDto
{
    /// <summary>Display name (may be renamed). Use RealName for AMI actions.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Canonical AMI queue name — additive for hide/rename.</summary>
    public string RealName { get; init; } = string.Empty;
    public string? Strategy { get; init; }
    public int Max { get; init; }
    public int CallsWaiting { get; init; }
    public int HoldtimeSeconds { get; init; }
    public int TalkTimeSeconds { get; init; }
    public int Completed { get; init; }
    public int Abandoned { get; init; }
    public int ServiceLevelSeconds { get; init; }
    public double ServiceLevelPerf { get; init; }
    /// <summary>Abandoned / (Completed + Abandoned) * 100 — additive.</summary>
    public double AbandonedPercent { get; init; }
    public int Weight { get; init; }
    public IReadOnlyList<QueueMemberDto> Members { get; init; } = Array.Empty<QueueMemberDto>();
    public IReadOnlyList<QueueEntryDto> Entries { get; init; } = Array.Empty<QueueEntryDto>();
    public DateTimeOffset SnapshotUtc { get; init; }
}

public sealed class QueueMemberDto
{
    public string Interface { get; init; } = string.Empty;
    public string? StateInterface { get; init; }
    public string? Name { get; init; }
    public string? Membership { get; init; }
    public int Penalty { get; init; }
    public int CallsTaken { get; init; }
    public long LastCallEpoch { get; init; }
    public int? LastCallAgoSeconds { get; init; }
    public int Status { get; init; }
    /// <summary>Human label for Status (+ InCall overlay) — additive.</summary>
    public string StatusLabel { get; init; } = string.Empty;
    public bool Paused { get; init; }
    public string? PausedReason { get; init; }
    public bool InCall { get; init; }
    public long LastPauseEpoch { get; init; }
    public int? LastPauseAgoSeconds { get; init; }
}

public sealed class QueueEntryDto
{
    public int Position { get; init; }
    public string? Channel { get; init; }
    public string? UniqueId { get; init; }
    public string? CallerId { get; init; }
    public string? CallerIdName { get; init; }
    public long WaitSeconds { get; init; }
    public int Priority { get; init; }
}

public sealed class QueueMemberPauseRequest
{
    public string Interface { get; set; } = string.Empty;
    public string? Queue { get; set; }
    public string? Reason { get; set; }
}

public sealed class QueueHangupEntryRequest
{
    public string Channel { get; set; } = string.Empty;
}
