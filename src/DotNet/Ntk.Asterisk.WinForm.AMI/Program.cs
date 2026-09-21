using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ntk.Asterisk.AMI;
using Ntk.Asterisk.Core;

namespace Ntk.Asterisk.WinForm.AMI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddAsteriskCore(builder.Configuration);
        builder.Services.AddAsteriskAmi();
        builder.Services.AddTransient<FormMain>();
        using var host = builder.Build();
        Application.Run(host.Services.GetRequiredService<FormMain>());
    }
}
