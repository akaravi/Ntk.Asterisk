using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core;
using Ntk.Asterisk.Monitoring;
using Ntk.Asterisk.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAsteriskAuditLog();
builder.Services.AddAsteriskCore(builder.Configuration);
builder.Services.AddAsteriskAmi();
builder.Services.AddAsteriskMonitoring();
builder.Services.AddHostedService<MonitoringWorker>();
await builder.Build().RunAsync();
