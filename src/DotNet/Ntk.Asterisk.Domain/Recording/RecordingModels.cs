using Ntk.Asterisk.Domain.Common;

namespace Ntk.Asterisk.Domain.Recording;

public enum RecordingFormat
{
    Wav = 0,
    Mp3 = 1,
    Gsm = 2,
    Wav49 = 3
}

public enum CallDirection
{
    Unknown = 0,
    Inbound = 1,
    Outbound = 2,
    Internal = 3
}

public class AudioRecording : Entity<string>, IAggregateRoot
{
    public string ServerId { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string UniqueId { get; private set; } = string.Empty;
    public string CallerIdNum { get; private set; } = string.Empty;
    public string CallerIdName { get; private set; } = string.Empty;
    public string ConnectedLineNum { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public string Context { get; private set; } = string.Empty;
    public CallDirection Direction { get; private set; } = CallDirection.Unknown;
    public string RelativePath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public RecordingFormat Format { get; private set; } = RecordingFormat.Wav;
    public long FileSizeBytes { get; private set; }
    public double DurationSeconds { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }

    private AudioRecording() { }

    public AudioRecording(
        string id,
        string serverId,
        string channel,
        string uniqueId,
        string callerIdNum,
        string callerIdName,
        string connectedLineNum,
        string extension,
        string context,
        CallDirection direction,
        string relativePath,
        string fileName,
        RecordingFormat format,
        DateTime startedAtUtc)
    {
        Id = id;
        ServerId = serverId;
        Channel = channel;
        UniqueId = uniqueId;
        CallerIdNum = callerIdNum;
        CallerIdName = callerIdName;
        ConnectedLineNum = connectedLineNum;
        Extension = extension;
        Context = context;
        Direction = direction;
        RelativePath = relativePath;
        FileName = fileName;
        Format = format;
        StartedAtUtc = startedAtUtc;
        IsActive = true;
    }

    public void Complete(DateTime endedAtUtc, long fileSizeBytes, double durationSeconds)
    {
        EndedAtUtc = endedAtUtc;
        FileSizeBytes = fileSizeBytes;
        DurationSeconds = durationSeconds;
        IsActive = false;
    }

    public void MarkArchived() => IsArchived = true;
}
