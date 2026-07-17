using Ntk.AsterNet.AMI.Manager.Action;

namespace Ntk.Asterisk.WebApi.Ami;

/// <summary>AMI MixMonitor — records bridged audio on a channel (res_mixmonitor).</summary>
public sealed class MixMonitorAction : ManagerAction
{
    public override string Action => "MixMonitor";

    public string Channel { get; set; } = string.Empty;

    /// <summary>Absolute or relative filename (without extension when Format implied by Asterisk).</summary>
    public string File { get; set; } = string.Empty;

    /// <summary>MixMonitor options, e.g. empty or "b" (only audio while bridged).</summary>
    public string? Options { get; set; }

    /// <summary>
    /// Shell command run on the Asterisk host after MixMonitor stops (AMI header Command).
    /// Used to push recording bytes into AstDB for remote download without mount/SSH.
    /// </summary>
    public string? Command { get; set; }
}

/// <summary>AMI StopMixMonitor.</summary>
public sealed class StopMixMonitorAction : ManagerAction
{
    public override string Action => "StopMixMonitor";

    public string Channel { get; set; } = string.Empty;

    public StopMixMonitorAction()
    {
    }

    public StopMixMonitorAction(string channel)
    {
        Channel = channel;
    }
}
