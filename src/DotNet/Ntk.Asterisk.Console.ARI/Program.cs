using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ntk.Asterisk.ARI;
using Ntk.Asterisk.Core;

namespace Ntk.Asterisk.Console.ARI;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddAsteriskCore(builder.Configuration);
        builder.Services.AddAsteriskAri();
        using var provider = builder.Services.BuildServiceProvider();
        try
        {
            var client = provider.GetRequiredService<IAriClientFactory>().CreateClient();
            client.Connect();
            System.Console.WriteLine("Connected to configured Asterisk ARI.");
            var recordings = client.Recordings.ListStored();
            foreach (var recording in recordings ?? [])
                System.Console.WriteLine($"Recording: {recording.Name}, format: {recording.Format}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"ARI unavailable: {ex.Message}");
        }
        finally
        {
            SafeWait();
        }
    }

    public static void SafeWait()
    {
        if (System.Console.IsInputRedirected || !Environment.UserInteractive) return;
        System.Console.WriteLine("Press any key to exit...");
        try { System.Console.ReadKey(); } catch (InvalidOperationException) { }
    }
}
