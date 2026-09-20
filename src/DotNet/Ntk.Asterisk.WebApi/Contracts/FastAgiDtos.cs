namespace Ntk.Asterisk.WebApi.Contracts;

public sealed class FastAgiPortStatusDto
{
    public int Port { get; set; }
    public bool IsListening { get; set; }
    public string Status { get; set; } = "Unknown"; // "Listening" | "Offline" | "Error"
    public string? ErrorMessage { get; set; }
    public DateTimeOffset LastCheckedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FastAgiStatusDto
{
    public List<FastAgiPortStatusDto> Ports { get; set; } = new();
    public int TotalPacketsReceived { get; set; }
    public DateTimeOffset? LastPacketAtUtc { get; set; }
    public string? LastClientIp { get; set; }
    public string OverallStatus { get; set; } = "Unknown";
}

public sealed class FastAgiPacketDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Port { get; set; } = 4573;
    public string ScriptName { get; set; } = string.Empty;
    public string CallerId { get; set; } = string.Empty;
    public string? CallerIdName { get; set; }
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string? Context { get; set; }
    public string? Extension { get; set; }
    public string? Priority { get; set; }
    public string? RemoteClientIp { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Commands { get; set; } = new();
    public string Status { get; set; } = "Processing"; // "Processing" | "Completed" | "Error"
    public string? Note { get; set; }
}

public sealed class FastAgiPacketReportRequest
{
    public string? PacketId { get; set; }
    public int Port { get; set; } = 4573;
    public string ScriptName { get; set; } = "smartroute";
    public string CallerId { get; set; } = string.Empty;
    public string? CallerIdName { get; set; }
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string? Context { get; set; }
    public string? Extension { get; set; }
    public string? Priority { get; set; }
    public string? RemoteClientIp { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? Command { get; set; }
    public string Status { get; set; } = "Completed";
    public string? Note { get; set; }
}
