var builder = DistributedApplication.CreateBuilder(args);
var servicesOnly = args.Any(arg => StringComparer.OrdinalIgnoreCase.Equals(arg, "--services-only") || StringComparer.OrdinalIgnoreCase.Equals(arg, "services-only"));

var elastic = builder.AddElasticsearch("Elasticsearch", port: 9200)
    .WithDataVolume(servicesOnly ? null : "exceptionless.data.v1");

if (!servicesOnly)
{
    elastic = elastic
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Elasticsearch")
        .WithKibana(b => b.WithLifetime(ContainerLifetime.Persistent).WithContainerName("Exceptionless-Kibana"));
}

var storage = builder.AddAzureStorage("Storage")
    .RunAsEmulator(c =>
    {
        if (!servicesOnly)
        {
            c.WithLifetime(ContainerLifetime.Persistent);
            c.WithContainerName("Exceptionless-Storage");
            c.WithDataVolume();
        }
    });

var storageBlobs = storage.AddBlobs("StorageBlobs");
var storageQueues = storage.AddQueues("StorageQueues");

var cache = builder.AddRedis("Redis", port: 6379)
    .WithImageTag("8.6")
    .WithClearCommand();

if (!servicesOnly)
{
    cache = cache
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Redis")
        .WithRedisInsight(b => b.WithLifetime(ContainerLifetime.Persistent).WithContainerName("Exceptionless-RedisInsight").WithUrlForEndpoint("http", u => u.DisplayText = "Cache"));
}

var mail = builder.AddContainer("Mail", "axllent/mailpit")
    .WithImageTag("v1.27.10")
    .WithHttpEndpoint(8025, 8025, "http")
    .WithUrlForEndpoint("http", u => u.DisplayText = "Mail")
    .WithHttpHealthCheck("/readyz")
    .WithEndpoint(1025, 1025);

if (!servicesOnly)
{
    mail = mail
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Mail");
}

if (!servicesOnly)
{
    builder.AddProject<Projects.Exceptionless_Job>("Jobs", "AllJobs")
        .WithReference(cache)
        .WithReference(elastic)
        .WithReference(storageBlobs, "AzureStorage")
        .WithReference(storageQueues, "AzureQueues")
        .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithUrlForEndpoint("http", u => u.DisplayText = "Jobs")
        .WithHttpHealthCheck("/health");

    var api = builder.AddProject<Projects.Exceptionless_Web>("Api", "Exceptionless")
        .WithReference(cache)
        .WithReference(elastic)
        .WithReference(storageBlobs, "AzureStorage")
        .WithReference(storageQueues, "AzureQueues")
        .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
        .WithEnvironment("RunJobsInProcess", "false")
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithUrlForEndpoint("http", u => u.DisplayText = "Api")
        .WithHttpHealthCheck("/health");

    builder.AddViteApp("Web", "../../src/Exceptionless.Web/ClientApp")
        .WithReference(api)
        .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5200")
        .WithEndpoint("http", e =>
        {
            e.Port = 5173;
            e.IsProxied = false;
        })
        .WithUrlForEndpoint("http", u => { u.DisplayText = "Web"; u.Url = "/next/"; });

    builder.AddJavaScriptApp("AngularWeb", "../../src/Exceptionless.Web/ClientApp.angular", "serve")
        .WithReference(api)
        .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5200")
        .WithUrlForEndpoint("AngularWeb", u => u.DisplayText = "Angular Web")
        .WithHttpEndpoint(port: 5100, targetPort: 5100, name: "AngularWeb", env: "PORT", isProxied: false);
}

await builder.Build().RunAsync();
