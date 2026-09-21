namespace Ntk.Asterisk.Domain.Monitoring;

public enum SpyMode
{
    Listen = 0,   // Silent listening (b = default)
    Whisper = 1,  // Whisper to spied channel only (w)
    Barge = 2,    // Barge / two-way audio (B)
    Coach = 3     // Whisper/Coach mode
}

public enum SpyDirection
{
    Both = 0,
    In = 1,
    Out = 2
}

public enum AudioInterventionAction
{
    Spy,
    Whisper,
    Barge,
    PauseRecording,
    ResumeRecording,
    MuteRecording,
    StopRecording
}
