using Ntk.AsterNet.AMI.Manager.Action;

namespace Ntk.Asterisk.WebApi.Ami;

/// <summary>AMI ConfbridgeStartMoh — play MOH to a one-party ConfBridge room.</summary>
public sealed class ConfbridgeStartMohAction : ManagerAction
{
    public ConfbridgeStartMohAction()
    {
    }

    public ConfbridgeStartMohAction(string conference, string? mohClass = null)
    {
        Conference = conference;
        Class = mohClass;
    }

    public override string Action => "ConfbridgeStartMoh";

    public string Conference { get; set; } = string.Empty;

    /// <summary>Optional MOH class (Asterisk 13+ when supported).</summary>
    public string? Class { get; set; }
}

/// <summary>AMI ConfbridgeStopMoh — stop waiting music when the second party joins.</summary>
public sealed class ConfbridgeStopMohAction : ManagerAction
{
    public ConfbridgeStopMohAction()
    {
    }

    public ConfbridgeStopMohAction(string conference)
    {
        Conference = conference;
    }

    public override string Action => "ConfbridgeStopMoh";

    public string Conference { get; set; } = string.Empty;
}
