namespace Ntk.Asterisk.Core.Contracts;

public class FastAgiLookupRequest
{
    public string CallerId { get; set; } = string.Empty;
    public string Did { get; set; } = string.Empty;
    public string? Context { get; set; }
    public string? UniqueId { get; set; }
    public string? Channel { get; set; }
}

public class FastAgiLookupResponse
{
    public bool RouteFound { get; set; }
    public string? Destination { get; set; }
    public string? RouteId { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string> ChannelVariables { get; set; } = [];
}

public class QueueAclUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // admin, manager, user
    public List<string> AllowedQueues { get; set; } = [];
    public bool CanSpy { get; set; }
    public bool CanRecord { get; set; }
}

public class QueueMemberSummaryDto
{
    public string MemberName { get; set; } = string.Empty;
    public string StateInterface { get; set; } = string.Empty;
    public int Status { get; set; }
    public bool Paused { get; set; }
    public int CallsTaken { get; set; }
    public long LastCall { get; set; }
}

public class QueueSummaryDto
{
    public string QueueName { get; set; } = string.Empty;
    public int CallsWaiting { get; set; }
    public int HoldTime { get; set; }
    public int TalkTime { get; set; }
    public int Completed { get; set; }
    public int Abandoned { get; set; }
    public double ServiceLevel { get; set; }
    public List<QueueMemberSummaryDto> Members { get; set; } = [];
}
