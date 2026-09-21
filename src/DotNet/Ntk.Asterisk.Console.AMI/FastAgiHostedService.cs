using Ntk.Asterisk.Console.AMI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ntk.AsterNet.AMI.FastAGI;
using Ntk.Asterisk.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Ntk.Asterisk.Console.AMI;

public sealed class FastAgiHostedService : BackgroundService
{
    private readonly IOptions<AsteriskOptions> _options;
    private readonly ILogger<FastAgiHostedService> _logger;
    private AsteriskFastAGI? _server;

    public FastAgiHostedService(
        IOptions<AsteriskOptions> options,
        ILogger<FastAgiHostedService> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _options.Value.Servers.FirstOrDefault()?.FastAgi
            ?? throw new InvalidOperationException("No FastAGI server configuration is available.");

        _server = new AsteriskFastAGI("127.0.0.1", config.ListenPort, config.MaxConcurrentConnections)
        {
            MappingStrategy = new ProductFastAgiMappingStrategy()
        };

        using var registration = stoppingToken.Register(static state => ((AsteriskFastAGI)state!).Stop(), _server);
        _logger.LogInformation("Starting FastAGI listener on 127.0.0.1:{Port}.", config.ListenPort);

        try
        {
            await Task.Run(() => _server.Start(), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FastAGI listener stopped unexpectedly on port {Port}.", config.ListenPort);
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Stop();
        return base.StopAsync(cancellationToken);
    }
}

internal sealed class ProductFastAgiMappingStrategy : IMappingStrategy
{
    private readonly IReadOnlyDictionary<string, AGIScript> _scripts =
        new Dictionary<string, AGIScript>(StringComparer.OrdinalIgnoreCase)
        {
            ["smartroute"] = new SmartCallRouteAgiScript(),
            ["customivr"] = new CustomIVR()
        };

    public AGIScript DetermineScript(AGIRequest request) =>
        _scripts.TryGetValue(request.Script, out var script) ? script : null!;

    public void Load()
    {
    }
}
