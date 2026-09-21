using Ntk.Asterisk.Domain.Monitoring;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Core.Contracts;

public class SpyChannelRequest
{
    public string ServerId { get; set; } = "default";
    public string TargetChannel { get; set; } = string.Empty;
    public string SpyingExtension { get; set; } = string.Empty;
    public SpyMode Mode { get; set; } = SpyMode.Listen;
    public SpyDirection Direction { get; set; } = SpyDirection.Both;
}

public class RecordChannelRequest
{
    public string ServerId { get; set; } = "default";
    public string Channel { get; set; } = string.Empty;
    public string? CustomFileName { get; set; }
    public RecordingFormat Format { get; set; } = RecordingFormat.Wav;
    public string? MixMonitorOptions { get; set; }
}

public class AudioRecordingDto
{
    public string Id { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string UniqueId { get; set; } = string.Empty;
    public string CallerIdNum { get; set; } = string.Empty;
    public string CallerIdName { get; set; } = string.Empty;
    public string ConnectedLineNum { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public CallDirection Direction { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public RecordingFormat Format { get; set; }
    public long FileSizeBytes { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
}

public class CallJobDto
{
    public string Id { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string CallerId { get; set; } = string.Empty;
    public int TimeoutMs { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class CreateCallJobRequest
{
    public string ServerId { get; set; } = "default";
    public string Channel { get; set; } = string.Empty;
    public string Context { get; set; } = "default";
    public string Extension { get; set; } = "s";
    public int Priority { get; set; } = 1;
    public string CallerId { get; set; } = "";
    public int TimeoutMs { get; set; } = 30000;
    public Dictionary<string, string>? Variables { get; set; }
}
