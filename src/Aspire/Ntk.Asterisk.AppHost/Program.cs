var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.Ntk_Asterisk_WebApi>("webapi");

var webPhone = builder.AddProject<Projects.Ntk_Asterisk_WebPhone>("webphone")
    .WithReference(webApi);

var adminDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../Angular/AdminPanel"));
var adminScript = Path.GetFullPath(Path.Combine(adminDir, "node_modules/@angular/cli/bin/ng.js"));

builder.AddNodeApp("admin-panel", adminDir, adminScript)
    .WithArgs("serve", "--port", "5314")
    .WithReference(webApi)
    .WithReference(webPhone)
    .WithHttpEndpoint(port: 5314, isProxied: false)
    .WithExternalHttpEndpoints();

var userDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../Angular/UserPanel"));
var userScript = Path.GetFullPath(Path.Combine(userDir, "node_modules/@angular/cli/bin/ng.js"));

builder.AddNodeApp("user-panel", userDir, userScript)
    .WithArgs("serve", "--port", "5312")
    .WithReference(webApi)
    .WithReference(webPhone)
    .WithHttpEndpoint(port: 5312, isProxied: false)
    .WithExternalHttpEndpoints();
builder.Build().Run();
