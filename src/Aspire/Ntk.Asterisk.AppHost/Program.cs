var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.Ntk_Asterisk_WebApi>("webapi");

builder.AddProject<Projects.Ntk_Asterisk_WebPhone>("webphone")
    .WithReference(webApi);

builder.AddProject<Projects.Asterisk_Console_AMI>("console-ami");

builder.AddProject<Projects.Asterisk_Console_ARI>("console-ari");

builder.AddProject<Projects.Asterisk_WinForm_AMI>("winform-ami")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Asterisk_WinForm_ARI>("winform-ari")
    .WithExternalHttpEndpoints();

builder.Build().Run();
