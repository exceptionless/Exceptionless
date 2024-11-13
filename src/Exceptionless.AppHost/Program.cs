var builder = DistributedApplication.CreateBuilder(args);

var elastic = builder.AddElasticsearch("Elasticsearch", port: 9200)
    .WithImageTag("8.15.2")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Elasticsearch")
    .WithDataVolume("exceptionless.data.v1")
    .WithKibana(b => b.WithLifetime(ContainerLifetime.Persistent).WithContainerName("Exceptionless-Kibana"));

var cache = builder.AddRedis("Redis", port: 6379)
    .WithImageTag("7.4")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Redis")
    .WithClearCommand()
    .WithRedisInsight(b => b.WithLifetime(ContainerLifetime.Persistent).WithContainerName("Exceptionless-RedisInsight"));

var mail = builder.AddContainer("Mail", "mailhog/mailhog")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Mail")
    .WithEndpoint(8025, 8025, "http")
    .WithEndpoint(1025, 1025);

builder.AddProject<Projects.Exceptionless_Job>("Jobs", "AllJobs")
    .WithReference(cache)
    .WithReference(elastic)
    .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
    .WaitFor(elastic)
    .WaitFor(cache)
    .WaitFor(mail)
    .WithHttpHealthCheck("/health");

var api = builder.AddProject<Projects.Exceptionless_Web>("Api", "Exceptionless")
    .WithReference(cache)
    .WithReference(elastic)
    .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
    .WithEnvironment("RunJobsInProcess", "false")
    .WaitFor(elastic)
    .WaitFor(cache)
    .WaitFor(mail)
    .WithHttpHealthCheck("/health");

builder.AddNpmApp("Web", "../../src/Exceptionless.Web/ClientApp", "dev")
    .WithReference(api)
    .WithEndpoint(scheme: "http", env: "PORT");

builder.AddNpmApp("AngularWeb", "../../src/Exceptionless.Web/ClientApp.angular", "serve")
    .WithReference(api)
    .WithEndpoint(scheme: "http", env: "PORT");

builder.Build().Run();
