var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.Ntk_Asterisk_WebApi>("webapi");

var webPhone = builder.AddProject<Projects.Ntk_Asterisk_WebPhone>("webphone")
    .WithReference(webApi);
var fastAgi = builder.AddProject<Projects.Ntk_Asterisk_Console_AMI>("fastagi");

var adminDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../Angular/AdminPanel"));

builder.AddNodeApp("admin-panel", adminDir, "node_modules/@angular/cli/bin/ng.js")
    .WithArgs("serve", "--port", "5314")
    .WithReference(webApi)
    .WithReference(webPhone)
    .WithHttpEndpoint(port: 5314, isProxied: false)
    .WithExternalHttpEndpoints();

var userDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../Angular/UserPanel"));

builder.AddNodeApp("user-panel", userDir, "node_modules/@angular/cli/bin/ng.js")
    .WithArgs("serve", "--port", "5312")
    .WithReference(webApi)
    .WithReference(webPhone)
    .WithHttpEndpoint(port: 5312, isProxied: false)
    .WithExternalHttpEndpoints();
builder.Build().Run();
