var builder = DistributedApplication.CreateBuilder(args);

var elastic = builder.AddElasticsearch("Elasticsearch")
    .WithKibana();

var cache = builder.AddRedis("Redis")
    .WithRedisCommander();

var job = builder.AddProject<Projects.Exceptionless_Job>("Jobs", "AllJobs")
    .WithReference(cache)
    .WithReference(elastic);

var api = builder.AddProject<Projects.Exceptionless_Web>("Api", "Exceptionless API")
    .WithReference(cache)
    .WithReference(elastic);

builder.AddNpmApp("Web", "../../src/Exceptionless.Web/ClientApp", "dev")
    .WithReference(api)
    .WithEndpoint(port: 5173, scheme: "http", env: "PORT");

builder.Build().Run();
