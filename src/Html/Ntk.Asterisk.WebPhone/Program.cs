using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Dynamic config so API base URL stays in appsettings (not hardcoded in static JS).
app.MapGet("/ntk-webphone-config.js", (IConfiguration cfg) =>
{
    var apiBase = (cfg["WebPhoneUi:ApiBaseUrl"] ?? string.Empty).TrimEnd('/');
    var payload = JsonSerializer.Serialize(new { apiBaseUrl = apiBase });
    var js = "window.NTK_WEBPHONE_CONFIG=" + payload + ";";
    return Results.Content(js, "application/javascript; charset=utf-8");
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
