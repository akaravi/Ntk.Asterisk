using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ntk.AsterNet.AMI.Manager.Action;

namespace Ntk.Asterisk.AMI;

public class MixMonitorMuteAction : ManagerAction
{
    public override string Action => "MixMonitorMute";
    public string Channel { get; set; } = string.Empty;
    public string Direction { get; set; } = "both"; // read, write, both
    public int State { get; set; } = 1; // 1 = mute, 0 = unmute

    public MixMonitorMuteAction() { }

    public MixMonitorMuteAction(string channel, string direction, int state)
    {
        Channel = channel;
        Direction = direction;
        State = state;
    }
}

public class StopMixMonitorAction : ManagerAction
{
    public override string Action => "StopMixMonitor";
    public string Channel { get; set; } = string.Empty;
    public string? MixMonitorID { get; set; }

    public StopMixMonitorAction() { }

    public StopMixMonitorAction(string channel, string? mixMonitorId = null)
    {
        Channel = channel;
        MixMonitorID = mixMonitorId;
    }
}

public class ConfbridgeSetSingleMohAction : ManagerAction
{
    public override string Action => "ConfbridgeSetSingleUser";
    public string Conference { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string UserProfile { get; set; } = "default_user";
}

public static class AmiServiceCollectionExtensions
{
    public static IServiceCollection AddAsteriskAmi(this IServiceCollection services)
    {
        services.TryAddSingleton<IAmiSession, AmiSession>();
        return services;
    }
}
