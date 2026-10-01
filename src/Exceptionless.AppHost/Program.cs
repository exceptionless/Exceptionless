using System.Reflection;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Hosting;

string? scope = WorktreeScope.Resolve();
bool isScoped = !String.IsNullOrWhiteSpace(scope);
var worktreePorts = isScoped ? WorktreeScope.AssignFreePorts() : null;
var builder = DistributedApplication.CreateBuilder(args);
IResourceBuilder<ParameterResource>? assistantApiKey = !String.IsNullOrWhiteSpace(builder.Configuration["Parameters:assistant-api-key"])
    ? builder.AddParameter("assistant-api-key", secret: true)
    : null;
bool servicesOnly = HasArgument("--services-only");
bool ciE2E = HasArgument("--ci-e2e");
bool includeDevTools = !ciE2E;
int oldAppHttpPort = worktreePorts?.OldAppHttp ?? 7120;
int oldAppPort = worktreePorts?.OldAppHttps ?? 7121;
int oldAppLiveReloadPort = worktreePorts?.OldAppLiveReload ?? 35729;
string oldAppAspNetCoreUrls = String.Concat("http://localhost:", oldAppHttpPort);
int appPort = worktreePorts?.AppHttps ?? 7131;
string appOrigin = worktreePorts?.AppHttpsUrl ?? $"https://web-ex.dev.localhost:{appPort}";
int docsPort = worktreePorts?.DocsHttp ?? 7141;
const int DefaultApiHttpsPort = 7111;
string exceptionlessServerUrl = worktreePorts?.ApiHttpsUrl ?? $"https://api-ex.dev.localhost:{DefaultApiHttpsPort}";
const string SharedEmailConnectionString = "smtp://localhost:1026";

var elastic = builder.AddElasticsearch("Elasticsearch", port: 9200)
    .WithDataVolume("exceptionless.data.v1")
    .WithEndpointProxySupport(false);

// Backend tests use in-memory queues/cache and scoped file storage. Starting the
// other shared containers wastes CI resources and can replace a running app's
// Azurite container when Aspire assigns different dynamic ports.
if (HasArgument("--test-services"))
{
    elastic.WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Elasticsearch");
    await builder.Build().RunAsync();
    return;
}

var storage = builder.AddAzureStorage("Storage")
    .RunAsEmulator(c =>
    {
        c.WithEndpointProxySupport(false);
        c.WithUrlForEndpoint("blob", u => { u.DisplayText = "Blobs"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });
        c.WithUrlForEndpoint("queue", u => { u.DisplayText = "Queues"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });
        c.WithUrlForEndpoint("table", u => { u.DisplayText = "Tables"; u.DisplayLocation = UrlDisplayLocation.DetailsOnly; });

        // Test runs must not replace another AppHost's emulator or share its queues.
        if (!ciE2E)
        {
            c.WithLifetime(ContainerLifetime.Persistent);
            c.WithContainerName("Exceptionless-Storage");
            c.WithDataVolume("exceptionless.storage.data.v1");
        }
    });

var storageBlobs = storage.AddBlobs("StorageBlobs");
var storageQueues = storage.AddQueues("StorageQueues");

// Aspire reserves 6380 for Redis's secondary non-TLS endpoint when proxying is disabled.
var cache = builder.AddRedis("Redis", port: ciE2E ? null : 6381)
    .WithImageTag("8.6")
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
    .WithHttpEndpoint(port: 8026, targetPort: 8025, name: "http")
    .WithUrlForEndpoint("http", u => { u.DisplayText = "Mail"; })
    .WithHttpHealthCheck("/readyz")
    .WithEndpoint(targetPort: 1025, port: 1026)
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
// Redis credentials and TLS configuration belong to one AppHost. CI sessions
// use isolated containers and allocated ports, including in local worktrees.
if (!ciE2E)
{
    cache = ownedCache
        .WithDataVolume("exceptionless.redis.data.v1")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithContainerName("Exceptionless-Redis");
}

if (!servicesOnly && includeDevTools)
{
    cache = cache.WithRedisInsight(b => b
        .WithLifetime(ContainerLifetime.Persistent)
        .WithEndpointProxySupport(false)
        .WithHostPort(5541)
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
        .WithEnvironment("Mcp:AllowedOrigins:0", appOrigin)
        .WithEnvironment("RunJobsInProcess", "false")
        .WaitFor(elastic)
        .WaitFor(cache)
        .WaitFor(mail)
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("https", u => { u.DisplayText = "Open API"; })
        .WithUrlForEndpoint("http", u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
        .WithHttpHealthCheck("/health");

    api.WithEnvironment("EX_ExceptionlessApiKey", builder.Configuration["ExceptionlessApiKey"])
        .WithEnvironment("EX_ExceptionlessServerUrl", api.GetEndpoint("http"));

    if (assistantApiKey is not null)
    {
        api.WithEnvironment("EX_Assistant__ApiKey", assistantApiKey);
    }

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
        .WithEnvironment("EX_ExceptionlessApiKey", builder.Configuration["ExceptionlessApiKey"])
        .WithEnvironment("EX_ExceptionlessServerUrl", api.GetEndpoint("http"))
        .WaitFor(api)
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
        })
        .WithParentRelationship(api);

    if (worktreePorts is not null)
    {
        oldApp.WithEnvironment("API_HTTP", worktreePorts.ApiHttpUrl)
            .WithEnvironment("API_HTTPS", worktreePorts.ApiHttpsUrl);
    }

    var app = builder.AddViteApp("App", "../Exceptionless.Web/ClientApp")
        .WithBrowserLogs()
        .WithReference(api)
        .WithReference(oldApp)
        .WithEnvironment("PUBLIC_EXCEPTIONLESS_API_KEY", builder.Configuration["PUBLIC_EXCEPTIONLESS_API_KEY"])
        .WithEnvironment("PUBLIC_EXCEPTIONLESS_SERVER_URL", exceptionlessServerUrl)
        .WithEnvironment("PUBLIC_EXCEPTIONLESS_TELEMETRY_SERVER_URL", builder.Configuration["PUBLIC_EXCEPTIONLESS_TELEMETRY_SERVER_URL"] ?? String.Empty)
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
        .WaitFor(api)
        .WithUrlForEndpoint("http", u =>
        {
            u.DisplayText = "Open App";
            u.Url = $"{u.Url.TrimEnd('/')}/next/";
        })
        .WithParentRelationship(api);

    if (ciE2E)
    {
        // CI/local test runners install the locked dependencies before startup.
        // A second npm install can rewrite the lockfile with a different npm version.
        oldApp.WithNpm(install: false);
        app.WithNpm(install: false);
    }

    if (worktreePorts is not null)
    {
        app.WithEnvironment("API_HTTP", worktreePorts.ApiHttpUrl)
            .WithEnvironment("API_HTTPS", worktreePorts.ApiHttpsUrl)
            .WithEnvironment("OLDAPP_HTTP", worktreePorts.OldAppHttpsUrl)
            .WithEnvironment("OLDAPP_HTTPS", worktreePorts.OldAppHttpsUrl);
    }

    if (includeDevTools)
    {
#pragma warning disable ASPIREDENO001
        builder.AddJavaScriptApp("Docs", "../../docs", "serve")
            .WithDeno()
            .WithBrowserLogs()
            .WithHttpEndpoint(port: docsPort, targetPort: docsPort, name: "http", env: "PORT", isProxied: false)
            .WithEndpoint("http", e =>
            {
                e.TargetHost = "localhost";
                e.UriScheme = "http";
            })
            .WithUrlForEndpoint("http", u =>
            {
                u.DisplayText = "Open Docs";
            })
            .WithParentRelationship(api);
#pragma warning restore ASPIREDENO001
    }
#pragma warning restore ASPIREBROWSERLOGS001
}

await builder.Build().RunAsync();

bool HasArgument(string name) => args.Any(arg => StringComparer.OrdinalIgnoreCase.Equals(arg, name) || StringComparer.OrdinalIgnoreCase.Equals(arg, name.TrimStart('-')));
