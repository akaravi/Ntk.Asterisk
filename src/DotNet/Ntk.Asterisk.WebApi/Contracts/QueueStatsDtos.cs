namespace Ntk.Asterisk.WebApi.Contracts;

public sealed class QueueStatsSampleDto
{
    public DateTimeOffset SnapshotUtc { get; init; }
    public string RealName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int CallsWaiting { get; init; }
    public int Completed { get; init; }
    public int Abandoned { get; init; }
    public double AbandonedPercent { get; init; }
    public int HoldtimeSeconds { get; init; }
    public int TalkTimeSeconds { get; init; }
    public double ServiceLevelPerf { get; init; }
    public int MemberCount { get; init; }
    public int PausedMemberCount { get; init; }
    public int InCallMemberCount { get; init; }
}
