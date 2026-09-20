var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.Ntk_Asterisk_WebApi>("webapi");

var webPhone = builder.AddProject<Projects.Ntk_Asterisk_WebPhone>("webphone")
    .WithReference(webApi);

builder.AddNodeApp("admin-panel", "node_modules/@angular/cli/bin/ng.js", "../../Angular/AdminPanel")
    .WithArgs("serve", "--port", "5314")
    .WithReference(webApi)
    .WithReference(webPhone)
    .WithHttpEndpoint(port: 5314, isProxied: false)
    .WithExternalHttpEndpoints();

builder.AddNodeApp("user-panel", "node_modules/@angular/cli/bin/ng.js", "../../Angular/UserPanel")
    .WithArgs("serve", "--port", "5312")
    .WithReference(webApi)
    .WithReference(webPhone)
    .WithHttpEndpoint(port: 5312, isProxied: false)
    .WithExternalHttpEndpoints();
builder.Build().Run();
