using Ntk.Asterisk.Domain.Common;

namespace Ntk.Asterisk.Domain.Asterisk;

public class ChannelSnapshot : Entity<string>
{
    public string ServerId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelState { get; set; } = string.Empty;
    public string ChannelStateDesc { get; set; } = string.Empty;
    public string CallerIdNum { get; set; } = string.Empty;
    public string CallerIdName { get; set; } = string.Empty;
    public string ConnectedLineNum { get; set; } = string.Empty;
    public string ConnectedLineName { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Application { get; set; } = string.Empty;
    public string ApplicationData { get; set; } = string.Empty;
    public string UniqueId { get; set; } = string.Empty;
    public string LinkedId { get; set; } = string.Empty;
    public string BridgeId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class PeerSnapshot : Entity<string>
{
    public string ServerId { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty; // PJSIP, SIP, IAX2
    public string Status { get; set; } = string.Empty; // OK, UNREACHABLE, UNKNOWN
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Dynamic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastStatusChangeUtc { get; set; } = DateTime.UtcNow;
}

public class QueueMemberSnapshot : Entity<string>
{
    public string QueueName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string StateInterface { get; set; } = string.Empty;
    public int Membership { get; set; }
    public int Penalty { get; set; }
    public int CallsTaken { get; set; }
    public long LastCall { get; set; }
    public int Status { get; set; }
    public bool Paused { get; set; }
    public string PausedReason { get; set; } = string.Empty;
}

public class QueueSnapshot : Entity<string>
{
    public string ServerId { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public int MaxCalls { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public int Calls { get; set; }
    public int HoldTime { get; set; }
    public int TalkTime { get; set; }
    public int Completed { get; set; }
    public int Abandoned { get; set; }
    public double ServiceLevel { get; set; }
    public double ServiceLevelPerf { get; set; }
    public int Weight { get; set; }
    public List<QueueMemberSnapshot> Members { get; set; } = [];
}
