using System.ComponentModel.DataAnnotations;
using Ntk.Asterisk.Domain.Monitoring;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Core.Configuration;

public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public int SnapshotIntervalMs { get; set; } = 1000;
    public bool TrackQueues { get; set; } = true;
    public bool TrackChannels { get; set; } = true;
    public bool TrackPeers { get; set; } = true;
    public string DefaultChannelTech { get; set; } = "PJSIP"; // PJSIP, SIP, IAX2
    public SpyMode SpyDefaultMode { get; set; } = SpyMode.Listen;
    public bool AclEnabled { get; set; } = true;
}

public class RecordingOptions
{
    public const string SectionName = "Recording";

    public string StorageProvider { get; set; } = "FileSystem";
    public FileSystemStorageOptions FileSystem { get; set; } = new();
    public RecordingRetentionOptions Retention { get; set; } = new();
}

public class FileSystemStorageOptions
{
    public string BasePath { get; set; } = "App_Data/recordings";
    public string PathFormat { get; set; } = "{Year}/{Month}/{Day}/{CallId}_{Direction}_{Caller}_{Called}.{Ext}";
    public RecordingFormat DefaultAudioFormat { get; set; } = RecordingFormat.Wav;
}

public class RecordingRetentionOptions
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 90;
    public int CleanupIntervalHours { get; set; } = 24;
    public bool AutoArchive { get; set; } = false;
}

public class QueueAclOptions
{
    public const string SectionName = "QueueAcl";
    public bool Enabled { get; set; } = true;
    public string UsersFilePath { get; set; } = "App_Data/queue-acl-users.json";
}

public class WebPhoneOptions
{
    public const string SectionName = "WebPhone";
    public string DataFilePath { get; set; } = "App_Data/webphone-extensions.json";
    public string RecordingsPath { get; set; } = "App_Data/webphone-recordings";
    public int TokenExpiryMinutes { get; set; } = 60;
}
