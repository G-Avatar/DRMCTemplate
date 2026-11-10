var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.WebApi>("webapi");

await builder.Build().RunAsync();
