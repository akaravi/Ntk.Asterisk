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
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCommandJob { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public CancellationTokenSource? Cts { get; set; }
}
