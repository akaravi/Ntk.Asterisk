namespace Ntk.Asterisk.Core.Contracts;

public sealed class ConnectionStatusDto
{
    public bool Connected { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Version { get; set; }
    public DateTime? LastConnectedUtc { get; set; }
    public string? LastError { get; set; }
    public string? ServerId { get; set; }
    public string? ServerTitle { get; set; }
    public bool IsDefault { get; set; }
    public bool Enabled { get; set; } = true;
    public string Protocol { get; set; } = "AMI";
}

public sealed class ConnectionServerRequest
{
    public string? ServerId { get; set; }
}

public sealed class PeerDto
{
    public string ServerId { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Dynamic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Monitored { get; set; }
}

public sealed class ChannelDto
{
    public string ServerId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
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
    public double DurationSeconds { get; set; }
}

public sealed class HangupRequest
{
    public string? ServerId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public int Cause { get; set; } = 16; // Normal clearing
}

public sealed class BridgeRequest
{
    public string? ServerId { get; set; }
    public string Channel1 { get; set; } = string.Empty;
    public string Channel2 { get; set; } = string.Empty;
    public bool Tone { get; set; } = true;
}

public sealed class ChanSpyRequest
{
    public string? ServerId { get; set; }
    public string TargetChannel { get; set; } = string.Empty;
    public string SpyingExtension { get; set; } = string.Empty;
    public string Mode { get; set; } = "listen"; // listen, quiet, whisper, privateWhisper, barge, dtmf
    public string? RecordPrefix { get; set; }
}

public sealed class LiveEventDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ServerId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? UniqueId { get; set; }
    public string? CallerIdNum { get; set; }
    public string? ConnectedLineNum { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Attributes { get; set; } = [];
}

public sealed class CallFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public string Status { get; set; } = "Spool";
}

public sealed class CallFileAddRequest
{
    public string? ServerId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Context { get; set; } = "default";
    public string Extension { get; set; } = "s";
    public int Priority { get; set; } = 1;
    public string CallerId { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public Dictionary<string, string>? SetVariables { get; set; }
}
