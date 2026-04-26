using System.Reflection;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);
var servicesOnly = args.Any(arg => StringComparer.OrdinalIgnoreCase.Equals(arg, "--services-only") || StringComparer.OrdinalIgnoreCase.Equals(arg, "services-only"));

var elastic = builder.AddElasticsearch("Elasticsearch", port: 9200)
    .WithDataVolume(servicesOnly ? null : "exceptionless.data.v1");

var storage = builder.AddAzureStorage("Storage")
    .RunAsEmulator(c =>
    {
        c.WithUrlForEndpoint("blob", u => { u.DisplayText = "Blobs"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });
        c.WithUrlForEndpoint("queue", u => { u.DisplayText = "Queues"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });
        c.WithUrlForEndpoint("table", u => { u.DisplayText = "Tables"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });

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
    .WithClearCommand()
    .WithUrls(c =>
    {
        foreach (var url in c.Urls)
        {
            url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
        }
    });

var mail = builder.AddContainer("Mail", "axllent/mailpit")
    .WithImageTag("v1.27.10")
    .WithHttpEndpoint(8025, 8025, "http")
    .WithUrlForEndpoint("http", u => { u.DisplayText = "Mail"; u.DisplayOrder = 100; })
    .WithHttpHealthCheck("/readyz")
    .WithEndpoint(1025, 1025)
    .WithUrlForEndpoint("tcp", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly);

if (!servicesOnly)
{
    elastic = elastic
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Elasticsearch")
        .WithKibana(b => b
            .WithLifetime(ContainerLifetime.Persistent)
            .WithContainerName("Exceptionless-Kibana")
            .WithParentRelationship(elastic));

    cache = cache
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Redis")
        .WithRedisInsight(b => b
            .WithLifetime(ContainerLifetime.Persistent)
            .WithContainerName("Exceptionless-RedisInsight")
            .WithUrlForEndpoint("http", u => u.DisplayText = "Redis")
            .WithParentRelationship(cache), containerName: "Redis-insight");

    mail = mail
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Mail");

    var api = builder.AddProject<Projects.Exceptionless_Web>("Api")
        .WithReference(cache)
        .WithReference(elastic)
        .WithReference(storageBlobs, "AzureStorage")
        .WithReference(storageQueues, "AzureQueues")
        .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
        .WithEnvironment("RunJobsInProcess", "false")
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("https", u => { u.DisplayText = "Open API"; u.DisplayOrder = 100; })
        .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
        .WithHttpHealthCheck("/health");

    builder.AddProject<Projects.Exceptionless_Job>("Jobs", "AllJobs")
        .WithReference(cache)
        .WithReference(elastic)
        .WithReference(storageBlobs, "AzureStorage")
        .WithReference(storageQueues, "AzureQueues")
        .WithEnvironment("ConnectionStrings:Email", "smtp://localhost:1025")
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithUrlForEndpoint("http", u => { u.DisplayText = "Jobs"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; })
        .WithUrlForEndpoint("https", u => { u.DisplayText = "Jobs"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; })
        .WithHttpHealthCheck("/health")
        .WithParentRelationship(api);

    var oldApp = builder.AddJavaScriptApp("OldApp", "../../src/Exceptionless.Web/ClientApp.angular", "serve")
        .WithReference(api)
        .RemoveJavaScriptDebuggingAnnotation()
        .WithEnvironment("ASPNETCORE_URLS", "http://localhost:5200")
        .WithEnvironment("USE_HTTPS", "true")
        .WithHttpEndpoint(port: 5100, targetPort: 5100, name: "https", env: "PORT", isProxied: false)
        .WithEndpoint("https", e =>
        {
            e.TargetHost = "angular-ex.dev.localhost";
            e.UriScheme = "https";
        })
        .WithHttpsDeveloperCertificate()
        .WithUrlForEndpoint("https", u => { u.DisplayText = "Open App (Old)"; u.DisplayOrder = 100; })
        .WithParentRelationship(api);

    builder.AddViteApp("App", "../Exceptionless.Web/ClientApp")
        .WithReference(api)
        .WithReference(oldApp)
        .RemoveJavaScriptDebuggingAnnotation()
        .WithEndpoint("http", e =>
        {
            e.Port = 5173;
            e.TargetPort = 5173;
            e.TargetHost = "web-ex.dev.localhost";
            e.IsProxied = false;
        })
        .WithHttpsDeveloperCertificate()
        .WithUrlForEndpoint("http", u =>
        {
            u.DisplayText = "Open App";
            u.DisplayOrder = 100;
            u.Url = $"{u.Url?.TrimEnd('/')}/next/";
        })
        .WithParentRelationship(api);
}

await builder.Build().RunAsync();
