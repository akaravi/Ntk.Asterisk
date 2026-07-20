using Ntk.Asterisk.WebApi.Ami;
using Ntk.Asterisk.WebApi.Configuration;
using Ntk.Asterisk.WebApi.Hubs;
using Ntk.Asterisk.WebApi.Jobs;
using Ntk.Asterisk.WebApi.Middleware;
using Ntk.Asterisk.WebApi.Services;
using CorsOpts = Ntk.Asterisk.WebApi.Configuration.CorsOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AsteriskOptions>(builder.Configuration.GetSection(AsteriskOptions.SectionName));
builder.Services.Configure<CorsOpts>(builder.Configuration.GetSection(CorsOpts.SectionName));
builder.Services.Configure<WebPhoneOptions>(builder.Configuration.GetSection(WebPhoneOptions.SectionName));
builder.Services.Configure<QueueAclOptions>(builder.Configuration.GetSection(QueueAclOptions.SectionName));
builder.Services.Configure<QueueStatsOptions>(builder.Configuration.GetSection(QueueStatsOptions.SectionName));

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
if (corsOrigins.Length == 0)
{
    corsOrigins =
    [
        "http://localhost:5312",
        "http://localhost:5314",
        "http://localhost:5316",
        "http://127.0.0.1:5312",
        "http://127.0.0.1:5314",
        "http://127.0.0.1:5316"
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Panels", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IAsteriskSettingsService, AsteriskSettingsService>();
builder.Services.AddHttpClient("recording-fetch", client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Issabel often serves recordings over HTTPS with a self-signed / mismatched cert.
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddSingleton<ICallRecordingService, CallRecordingService>();
builder.Services.AddSingleton<ICallFileService, CallFileService>();

builder.Services.AddSingleton<AmiSession>();
builder.Services.AddSingleton<IAmiSession>(sp => sp.GetRequiredService<AmiSession>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AmiSession>());

builder.Services.AddSingleton<ICallJobStore, CallJobStore>();
builder.Services.AddSingleton<CallJobEngine>();
builder.Services.AddSingleton<ICallJobEngine>(sp => sp.GetRequiredService<CallJobEngine>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CallJobEngine>());

builder.Services.AddSingleton<MonitorService>();
builder.Services.AddSingleton<IMonitorService>(sp => sp.GetRequiredService<MonitorService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MonitorService>());

builder.Services.AddSingleton<QueueMonitorService>();
builder.Services.AddSingleton<IQueueMonitorService>(sp => sp.GetRequiredService<QueueMonitorService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<QueueMonitorService>());

builder.Services.AddSingleton<IQueueAclStore, QueueAclStore>();
builder.Services.AddSingleton<IQueueAclSessionService, QueueAclSessionService>();
builder.Services.AddSingleton<IQueueAclConnectionRegistry, QueueAclConnectionRegistry>();
builder.Services.AddSingleton<IQueueStatsSnapshotStore, QueueStatsSnapshotStore>();
builder.Services.AddHostedService<QueueStatsSamplerService>();

builder.Services.AddSingleton<IWebPhoneExtensionStore, WebPhoneExtensionStore>();
builder.Services.AddSingleton<IWebPhoneProvisionTokenStore, WebPhoneProvisionTokenStore>();
builder.Services.AddSingleton<IWebPhoneBuddyStore, WebPhoneBuddyStore>();
builder.Services.AddSingleton<IWebPhoneCdrStore, WebPhoneCdrStore>();
builder.Services.AddSingleton<IWebPhoneRecordingStore, WebPhoneRecordingStore>();
builder.Services.AddSingleton<IWebPhoneQosStore, WebPhoneQosStore>();
builder.Services.AddSingleton<WebPhonePresenceService>();
builder.Services.AddSingleton<IWebPhonePresenceService>(sp => sp.GetRequiredService<WebPhonePresenceService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebPhonePresenceService>());

builder.Services.AddSingleton<LiveEventSinkHolder>();
builder.Services.AddSingleton<AmiLiveEventFeed>();
builder.Services.AddSingleton<ILiveEventFeed>(sp => sp.GetRequiredService<AmiLiveEventFeed>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AmiLiveEventFeed>());
builder.Services.AddSingleton<ILoggerProvider, LiveEventLoggerProvider>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Panels");
app.UseMiddleware<QueueAclGateMiddleware>();

// Softphone UI lives in src/WebPhone/Ntk.Asterisk.WebPhone — WebApi is API-only (no wwwroot UI).

app.MapControllers();
app.MapHub<AsteriskHub>(AsteriskHub.Path);

app.Run();
