using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ntk.Asterisk.Core.Configuration;
using Ntk.Asterisk.Core.Persistence;
using Ntk.Asterisk.Core.Services;
using Ntk.Asterisk.Domain.CallJobs;
using Ntk.Asterisk.Domain.CallRouting;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddAsteriskCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AsteriskOptions>().Bind(configuration.GetSection(AsteriskOptions.SectionName)).ValidateDataAnnotations().Validate(o => o.Servers.Count > 0, "At least one Asterisk server must be configured.").ValidateOnStart();
        services.AddOptions<MonitoringOptions>().Bind(configuration.GetSection(MonitoringOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RecordingOptions>().Bind(configuration.GetSection(RecordingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<QueueAclOptions>().Bind(configuration.GetSection(QueueAclOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<WebPhoneOptions>().Bind(configuration.GetSection(WebPhoneOptions.SectionName));
        services.TryAddSingleton<ICallRouteRepository, InMemoryCallRouteRepository>();
        services.TryAddSingleton<ICallJobRepository, InMemoryCallJobRepository>();
        services.TryAddSingleton<IRecordingMetadataRepository, InMemoryRecordingMetadataRepository>();
        services.TryAddScoped<ICallRouteService, CallRouteService>();
        services.TryAddScoped<ICallJobService, CallJobService>();
        services.TryAddSingleton<IFastAgiTelemetryService, FastAgiTelemetryService>();
        return services;
    }
}
