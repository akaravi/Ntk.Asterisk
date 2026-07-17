namespace Ntk.Asterisk.WebApi.Jobs;

public enum CallJobType
{
    ExtToExt,
    MobileToExt,
    MobileToMobile,
    CommandHangup,
    CommandBridge
}

public enum CallJobState
{
    Queued,
    DialingLeg1,
    WaitingAnswer,
    DialingLeg2,
    Bridged,
    Completed,
    Failed,
    Cancelled
}

public sealed class CallJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public CallJobType Type { get; init; }
    public CallJobState State { get; set; } = CallJobState.Queued;
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Mobile1 { get; set; }
    public string? Mobile2 { get; set; }
    public string? Trunk { get; set; }
    public string? CallerId { get; set; }
    public int TimeoutMs { get; set; } = 30000;
    public string? ActionId { get; set; }
    public string? ActionIdLeg2 { get; set; }
    public string? Channel { get; set; }
    public string? Channel2 { get; set; }
    /// <summary>Resolved SIP/PJSIP media channels bridged for two-way audio (Local context).</summary>
    public string? MediaChannel1 { get; set; }
    public string? MediaChannel2 { get; set; }
    /// <summary>SIP/PJSIP channels observed via BridgeEnter/Newchannel for this call tree.</summary>
    public HashSet<string> ObservedSipChannels { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>CHANNEL(linkedid) / Originate UniqueId for correlating trunk channels.</summary>
    public string? LinkedId { get; set; }
    /// <summary>Channel MixMonitor was started on.</summary>
    public string? RecordingChannel { get; set; }
    /// <summary>Basename without path, e.g. ntk-{jobId}.wav</summary>
    public string? RecordingFileName { get; set; }
    public bool RecordingStarted { get; set; }
    public bool RecordingAvailable { get; set; }
    /// <summary>Application Data string used for Originate Dial (for diagnostics).</summary>
    public string? OriginateDialData { get; set; }
    public string? UniqueId { get; set; }
    public string? UniqueId2 { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>Human-readable outcome for success or failure (shown in job lists).</summary>
    public string? ResultReason { get; set; }
    public bool IsCommandJob { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>When dialing / command execution actually started.</summary>
    public DateTimeOffset? StartedAtUtc { get; set; }
    /// <summary>When job reached a terminal state.</summary>
    public DateTimeOffset? EndedAtUtc { get; set; }
    /// <summary>Wall-clock seconds from start to end when both are known.</summary>
    public int? DurationSeconds { get; set; }
    public CancellationTokenSource? Cts { get; set; }
}
