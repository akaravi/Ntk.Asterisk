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

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
if (corsOrigins.Length == 0)
{
    corsOrigins =
    [
        "http://localhost:5312",
        "http://localhost:5314",
        "http://127.0.0.1:5312",
        "http://127.0.0.1:5314"
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

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Panels");
app.MapControllers();
app.MapHub<AsteriskHub>(AsteriskHub.Path);

app.Run();
