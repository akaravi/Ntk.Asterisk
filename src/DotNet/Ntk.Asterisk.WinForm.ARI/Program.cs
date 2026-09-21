using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ntk.Asterisk.ARI;
using Ntk.Asterisk.Core;

namespace Ntk.Asterisk.WinForm.ARI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddAsteriskCore(builder.Configuration);
        builder.Services.AddAsteriskAri();
        builder.Services.AddTransient<FormMain>();
        using var host = builder.Build();
        Application.Run(host.Services.GetRequiredService<FormMain>());
    }
}
