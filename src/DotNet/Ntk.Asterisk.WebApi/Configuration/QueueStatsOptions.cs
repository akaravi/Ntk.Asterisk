namespace Ntk.Asterisk.WebApi.Configuration;

/// <summary>Live-queue snapshot retention for historical charts (Q14) — not Asterisk queue_log SQL.</summary>
public sealed class QueueStatsOptions
{
    public const string SectionName = "QueueStats";

    /// <summary>Sample interval minutes (minimum 1).</summary>
    public int SnapshotIntervalMinutes { get; set; } = 5;

    /// <summary>Keep samples for this many days.</summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Hard cap on stored samples (ring trim).</summary>
    public int MaxSamples { get; set; } = 50_000;
}
