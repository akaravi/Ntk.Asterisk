using System;
using Ntk.AsterNet.AMI.FastAGI;

namespace Ntk.Asterisk.Console.AMI { /// <summary>
/// FastAGI script entrypoint for Scenario 1.
/// Delegates to Scenario1_DirectFastAgiScenario (derived from SmartRouteScenarioBase).
/// </summary>
public class SmartCallRouteAgiScript : AGIScript
{
    private readonly Scenario1_DirectFastAgiScenario _scenario = new Scenario1_DirectFastAgiScenario();

    public string ApiBaseUrl
    {
        get => _scenario.ApiBaseUrl;
        set => _scenario.ApiBaseUrl = value;
    }

    public override void Service(AGIRequest request, AGIChannel channel)
    {
        _scenario.ExecuteAgiFlow(request, channel);
    }
} }
