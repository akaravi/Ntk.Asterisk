using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.AuditLog;
using Ntk.Asterisk.Core;

namespace Ntk.Asterisk.Console.AMI;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddAsteriskAuditLog();
        builder.Services.AddAsteriskCore(builder.Configuration);
        builder.Services.AddAsteriskAmi();
        builder.Services.AddHostedService<FastAgiHostedService>();
        using var host = builder.Build();
        try
        {
            var ami = host.Services.GetRequiredService<IAmiSession>();
            var status = await ami.TestConnectionAsync();
            System.Console.WriteLine(status.Connected
                ? $"Connected to configured AMI server '{status.ServerId}'."
                : $"AMI unavailable: {status.LastError}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"AMI unavailable: {ex.Message}");
        }

        await host.RunAsync();
}
}
