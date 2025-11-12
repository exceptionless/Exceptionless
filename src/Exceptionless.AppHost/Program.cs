var builder = DistributedApplication.CreateBuilder(args);

var elastic = builder.AddElasticsearch("Elasticsearch", port: 9200)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Elasticsearch")
    .WithDataVolume("exceptionless.data.v1")
    .WithKibana(b => b.WithLifetime(ContainerLifetime.Persistent).WithContainerName("Exceptionless-Kibana"));

var storage = builder.AddMinIo("Storage", s => s.WithCredentials("guest", "password").WithPorts(9000).WithBucket("ex-events"))
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Storage");

var cache = builder.AddRedis("Redis", port: 6379)
    .WithImageTag("7.4")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Redis")
    .WithClearCommand()
    .WithRedisInsight(b => b.WithLifetime(ContainerLifetime.Persistent).WithContainerName("Exceptionless-RedisInsight").WithUrlForEndpoint("http", u => u.DisplayText = "Cache"));

var mail = builder.AddContainer("Mail", "axllent/mailpit")
    .WithImageTag("v1.27.10")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Mail")
    .WithEndpoint(8025, 8025, "http")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Mail")
    .WithEndpoint(1025, 1025);

builder.AddProject<Projects.Exceptionless_Job>("Jobs", "AllJobs")
    .WithReference(cache)
    .WithReference(elastic)
    .WithReference(storage, "MinIO")
    .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
    .WaitFor(elastic)
    .WaitFor(cache)
    .WaitFor(mail)
    .WithUrlForEndpoint("http", u => u.DisplayText = "Jobs")
    .WithHttpHealthCheck("/health");

var api = builder.AddProject<Projects.Exceptionless_Web>("Api", "Exceptionless")
    .WithReference(cache)
    .WithReference(elastic)
    .WithReference(storage, "MinIO")
    .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
    .WithEnvironment("RunJobsInProcess", "false")
    .WaitFor(elastic)
    .WaitFor(cache)
    .WaitFor(mail)
    .WithUrlForEndpoint("http", u => u.DisplayText = "Api")
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("Web", "../../src/Exceptionless.Web/ClientApp", "dev")
    .WithReference(api)
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5200")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Web")
    .WithHttpEndpoint(port: 5173, env: "PORT", isProxied: false);

builder.AddJavaScriptApp("AngularWeb", "../../src/Exceptionless.Web/ClientApp.angular", "serve")
    .WithReference(api)
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5200")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Angular Web")
    .WithHttpEndpoint(port: 5100, env: "PORT", isProxied: false);

builder.Build().Run();
