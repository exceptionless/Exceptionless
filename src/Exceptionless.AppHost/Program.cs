var builder = DistributedApplication.CreateBuilder(args);

var elastic = builder.AddElasticsearch("Elasticsearch")
    .WithKibana();

var cache = builder.AddRedis("Redis")
    .WithRedisCommander();

var job = builder.AddProject<Projects.Exceptionless_Job>("Jobs")
    .WithReference(cache)
    .WithReference(elastic)
    .WithLaunchProfile("AllJobs");

var api = builder.AddProject<Projects.Exceptionless_Web>("Api")
    .WithReference(cache)
    .WithReference(elastic)
    .WithLaunchProfile("Exceptionless API");

builder.Build().Run();
