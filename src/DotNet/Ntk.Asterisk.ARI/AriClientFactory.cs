using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ntk.AsterNet.ARI;
using Ntk.Asterisk.Core.Configuration;

namespace Ntk.Asterisk.ARI;

public interface IAriClientFactory
{
    AriClient CreateClient(string? serverId = null);
}

public sealed class AriClientFactory : IAriClientFactory
{
    private readonly IOptionsMonitor<AsteriskOptions> _options;
    private readonly ILogger<AriClientFactory> _logger;

    public AriClientFactory(IOptionsMonitor<AsteriskOptions> options, ILogger<AriClientFactory> logger)
    {
        _options = options;
        _logger = logger;
    }

    public AriClient CreateClient(string? serverId = null)
    {
        var config = _options.CurrentValue.GetServer(serverId) ?? throw new InvalidOperationException($"Asterisk server '{serverId ?? "default"}' is not configured.");
        if (!Uri.TryCreate(config.Ari.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Asterisk:Ari:BaseUrl must be an absolute HTTP(S) URL.");
        if (string.IsNullOrWhiteSpace(config.Ari.Username) || string.IsNullOrWhiteSpace(config.Ari.Password))
            throw new InvalidOperationException($"ARI credentials are required for server '{config.Id}' via environment, user-secrets, or a secret store.");
        var endpoint = new StasisEndpoint(uri.Host, uri.Port, config.Ari.Username, config.Ari.Password);
        _logger.LogInformation("Creating configured ARI client for {ServerId}.", config.Id);
        return new AriClient(endpoint, config.Ari.Application);
    }
}

public static class AriServiceCollectionExtensions
{
    public static IServiceCollection AddAsteriskAri(this IServiceCollection services)
    {
        services.TryAddSingleton<IAriClientFactory, AriClientFactory>();
        return services;
    }
}
