using System.Reflection;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Hosting;

string? scope = WorktreeScope.Resolve();
bool isScoped = !String.IsNullOrWhiteSpace(scope);
var worktreePorts = isScoped ? WorktreeScope.AssignFreePorts() : null;
var builder = DistributedApplication.CreateBuilder(args);
bool servicesOnly = HasArgument("--services-only");
bool ciE2E = HasArgument("--ci-e2e");
bool includeDevTools = !ciE2E;
int oldAppHttpPort = worktreePorts?.OldAppHttp ?? 7120;
int oldAppPort = worktreePorts?.OldAppHttps ?? 7121;
int oldAppLiveReloadPort = worktreePorts?.OldAppLiveReload ?? 35729;
string oldAppAspNetCoreUrls = String.Concat("http://localhost:", oldAppHttpPort);
int appPort = worktreePorts?.AppHttps ?? 7131;
const int DefaultApiHttpsPort = 7111;
const string ClientSetupShowServerUrl = "true";
string exceptionlessServerUrl = worktreePorts?.ApiHttpsUrl ?? $"https://api-ex.dev.localhost:{DefaultApiHttpsPort}";
const string SharedEmailConnectionString = "smtp://localhost:1025";

var elastic = builder.AddElasticsearch("Elasticsearch", port: 9200)
    .WithDataVolume("exceptionless.data.v1")
    .WithEndpointProxySupport(false);

var storage = builder.AddAzureStorage("Storage")
    .RunAsEmulator(c =>
    {
        c.WithEndpointProxySupport(false);
        c.WithUrlForEndpoint("blob", u => { u.DisplayText = "Blobs"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });
        c.WithUrlForEndpoint("queue", u => { u.DisplayText = "Queues"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });
        c.WithUrlForEndpoint("table", u => { u.DisplayText = "Tables"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });

        c.WithLifetime(ContainerLifetime.Persistent);
        c.WithContainerName("Exceptionless-Storage");
        c.WithDataVolume("exceptionless.storage.data.v1");
    });

var storageBlobs = storage.AddBlobs("StorageBlobs");
var storageQueues = storage.AddQueues("StorageQueues");

var cache = builder.AddRedis("Redis", port: 6379)
    .WithImageTag("8.6")
    .WithDataVolume("exceptionless.redis.data.v1")
    .WithEndpointProxySupport(false)
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
    .WithEndpointProxySupport(false)
    .WithHttpEndpoint(8025, 8025, "http")
    .WithUrlForEndpoint("http", u => { u.DisplayText = "Mail"; u.DisplayOrder = 100; })
    .WithHttpHealthCheck("/readyz")
    .WithEndpoint(1025, 1025)
    .WithUrlForEndpoint("tcp", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly);

var ownedElastic = elastic;
elastic = ownedElastic
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Elasticsearch");

if (!servicesOnly && includeDevTools)
{
    elastic = elastic.WithKibana(b => b
        .WithLifetime(ContainerLifetime.Persistent)
        .WithEndpointProxySupport(false)
        .WithContainerName("Exceptionless-Kibana")
        .WithParentRelationship(ownedElastic));
}

var ownedCache = cache;
cache = ownedCache
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Redis");

if (!servicesOnly && includeDevTools)
{
    cache = cache.WithRedisInsight(b => b
        .WithLifetime(ContainerLifetime.Persistent)
        .WithEndpointProxySupport(false)
        .WithContainerName("Exceptionless-RedisInsight")
        .WithUrlForEndpoint("http", u => u.DisplayText = "Redis")
        .WithParentRelationship(ownedCache), containerName: "Redis-insight");
}

mail = mail
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("Exceptionless-Mail");

if (!servicesOnly)
{
    var api = builder.AddProject<Projects.Exceptionless_Web>("Api")
        .WithReference(cache)
        .WithReference(elastic)
        .WithReference(storageBlobs, "AzureStorage")
        .WithReference(storageQueues, "AzureQueues")
        .WithEnvironment("ConnectionStrings:Email", SharedEmailConnectionString)
        .WithEnvironment("RunJobsInProcess", "false")
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("https", u => { u.DisplayText = "Open API"; u.DisplayOrder = 100; })
        .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
        .WithHttpHealthCheck("/health");

    if (worktreePorts is not null)
    {
        api.WithEnvironment("Scope", scope!)
            .WithEnvironment("AppScope", scope!)
            .WithEndpoint("http", e => e.Port = worktreePorts.ApiHttp)
            .WithEndpoint("https", e => e.Port = worktreePorts.ApiHttps);
    }

    var jobs = builder.AddProject<Projects.Exceptionless_Job>("Jobs", "AllJobs")
        .WithReference(cache)
        .WithReference(elastic)
        .WithReference(storageBlobs, "AzureStorage")
        .WithReference(storageQueues, "AzureQueues")
        .WithEnvironment("ConnectionStrings:Email", SharedEmailConnectionString)
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithUrlForEndpoint("http", u =>
        {
            u.DisplayText = "Jobs";
            u.DisplayLocation = UrlDisplayLocation.DetailsOnly;
        })
        .WithUrlForEndpoint("https", u =>
        {
            u.DisplayText = "Jobs";
            u.DisplayLocation = UrlDisplayLocation.DetailsOnly;
        })
        .WithHttpHealthCheck("/health")
        .WithParentRelationship(api);

    if (worktreePorts is not null)
    {
        jobs.WithEnvironment("Scope", scope!)
            .WithEnvironment("AppScope", scope!)
            .WithEndpoint("http", e => e.Port = worktreePorts.JobsHttp);
    }

#pragma warning disable ASPIREBROWSERLOGS001
    var oldApp = builder.AddJavaScriptApp("OldApp", "../../src/Exceptionless.Web/ClientApp.angular", "serve")
        .WithBrowserLogs()
        .WithReference(api)
        .RemoveJavaScriptDebuggingAnnotation()
        .WithEnvironment("ASPNETCORE_URLS", oldAppAspNetCoreUrls)
        .WithEnvironment("USE_HTTPS", "true")
        .WithEnvironment("LIVERELOAD_PORT", oldAppLiveReloadPort.ToString())
        .WithHttpEndpoint(port: oldAppPort, targetPort: oldAppPort, name: "https", env: "PORT", isProxied: false)
        .WithEndpoint("https", e =>
        {
            e.TargetHost = "angular-ex.dev.localhost";
            e.UriScheme = "https";
        })
        .WithHttpsDeveloperCertificate()
        .WithUrlForEndpoint("https", u =>
        {
            u.DisplayText = "Open App (Old)";
            u.DisplayOrder = 100;
        })
        .WithParentRelationship(api);

    if (worktreePorts is not null)
    {
        oldApp.WithEnvironment("API_HTTP", worktreePorts.ApiHttpUrl)
            .WithEnvironment("API_HTTPS", worktreePorts.ApiHttpsUrl);
    }
#pragma warning restore ASPIREBROWSERLOGS001

#pragma warning disable ASPIREBROWSERLOGS001
    var app = builder.AddViteApp("App", "../Exceptionless.Web/ClientApp")
        .WithBrowserLogs()
        .WithReference(api)
        .WithReference(oldApp)
        .RemoveJavaScriptDebuggingAnnotation()
        .WithEnvironment("EX_ClientSetupShowServerUrl", ClientSetupShowServerUrl)
        .WithEnvironment("PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL", ClientSetupShowServerUrl)
        .WithEnvironment("PUBLIC_EXCEPTIONLESS_SERVER_URL", exceptionlessServerUrl)
        .WithEnvironment("PORT", appPort.ToString())
        .WithEndpoint("http", e =>
        {
            // 7131 (HTTPS via Aspire dev cert) instead of Vite's default 5173 to avoid clashing with other local Vite projects.
            e.Port = appPort;
            e.TargetPort = appPort;
            e.TargetHost = "web-ex.dev.localhost";
            e.IsProxied = false;
        })
        .WithHttpsDeveloperCertificate()
        .WithUrlForEndpoint("http", u =>
        {
            u.DisplayText = "Open App";
            u.DisplayOrder = 100;
            u.Url = $"{u.Url.TrimEnd('/')}/next/";
        })
        .WithParentRelationship(api);

    if (worktreePorts is not null)
    {
        app.WithEnvironment("API_HTTP", worktreePorts.ApiHttpUrl)
            .WithEnvironment("API_HTTPS", worktreePorts.ApiHttpsUrl)
            .WithEnvironment("OLDAPP_HTTP", worktreePorts.OldAppHttpsUrl)
            .WithEnvironment("OLDAPP_HTTPS", worktreePorts.OldAppHttpsUrl);
    }
#pragma warning restore ASPIREBROWSERLOGS001
}

await builder.Build().RunAsync();

bool HasArgument(string name) => args.Any(arg => StringComparer.OrdinalIgnoreCase.Equals(arg, name) || StringComparer.OrdinalIgnoreCase.Equals(arg, name.TrimStart('-')));
