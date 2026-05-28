using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Exceptionless.Tests;

public class AppWebHostFactory : WebApplicationFactory<Startup>, IAsyncLifetime
{
    private static readonly string[] s_indexPrefixes = ["events", "migrations", "organizations", "projects", "saved-views", "stacks", "tokens", "users", "webhooks"];
    private static readonly string s_runScope = $"test-{Guid.NewGuid().ToString("N")[..8]}";
    private static int s_counter = -1;
    private static readonly ConcurrentQueue<int> s_pool = new();
    private static readonly Lazy<Task<SharedApplicationContext>> s_sharedApplication = new(StartSharedApplicationAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    private bool _sliceReleased;

    public AppWebHostFactory()
    {
        if (!s_pool.TryDequeue(out var instanceId))
            instanceId = Interlocked.Increment(ref s_counter);

        InstanceId = instanceId;
        AppScope = instanceId == 0 ? s_runScope : $"{s_runScope}-{instanceId}";
    }

    public string AppScope { get; }
    public int InstanceId { get; }
    public bool IndexesHaveBeenConfigured { get; set; }

    public async ValueTask InitializeAsync()
    {
        var sharedApplication = await s_sharedApplication.Value;
        await CleanupElasticsearchSliceAsync(sharedApplication.ElasticsearchUri);
    }

    private static async Task<SharedApplicationContext> StartSharedApplicationAsync()
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(ElasticsearchResource).Assembly.FullName, DisableDashboard = true };
        var builder = DistributedApplication.CreateBuilder(options);

        // don't use random ports for tests
        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";

        var elasticsearch = builder.AddElasticsearch("Elasticsearch", port: 9200)
            .WithContainerName("Exceptionless-Elasticsearch-Test")
            .WithLifetime(ContainerLifetime.Persistent);

        var app = builder.Build();

        await app.StartAsync();

        var connectionString = await elasticsearch.Resource.GetConnectionStringAsync()
            ?? throw new InvalidOperationException("Could not resolve Elasticsearch connection string.");
        var elasticsearchUri = new Uri(connectionString);
        await WaitForElasticsearchAsync(elasticsearchUri);

        return new SharedApplicationContext(app, elasticsearchUri);
    }

    private static async Task WaitForElasticsearchAsync(Uri elasticsearchUri)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromSeconds(60);

        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            try
            {
                using var response = await client.GetAsync(elasticsearchUri);
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException("Timed out waiting for Elasticsearch test container to be ready.");
    }

    private async Task CleanupElasticsearchSliceAsync(Uri elasticsearchUri)
    {
        await WaitForElasticsearchAsync(elasticsearchUri);

        using var client = new HttpClient
        {
            BaseAddress = elasticsearchUri,
            Timeout = TimeSpan.FromSeconds(10)
        };

        foreach (string pattern in s_indexPrefixes.Select(prefix => Uri.EscapeDataString($"{AppScope}-{prefix}*")))
        {
            using var listResponse = await client.GetAsync($"/_cat/indices/{pattern}?h=index&format=json&expand_wildcards=all");
            if (listResponse.StatusCode == HttpStatusCode.NotFound)
                continue;

            listResponse.EnsureSuccessStatusCode();

            string payloadJson = await listResponse.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<List<CatIndexRecord>>(payloadJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
                ?? [];

            foreach (string indexName in payload.Select(record => record.Index).Where(name => !String.IsNullOrEmpty(name)).Distinct())
            {
                using var deleteResponse = await client.DeleteAsync($"/{Uri.EscapeDataString(indexName)}?ignore_unavailable=true");
                if (deleteResponse.StatusCode != HttpStatusCode.NotFound)
                    deleteResponse.EnsureSuccessStatusCode();
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSolutionRelativeContentRoot("src/Exceptionless.Web", "*.slnx");
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppScope"] = AppScope
            })
            .Build();

        return Web.Program.CreateHostBuilder(config, Environments.Development);
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            if (!_sliceReleased)
            {
                s_pool.Enqueue(InstanceId);
                _sliceReleased = true;
            }
        }
    }

    private sealed record SharedApplicationContext(DistributedApplication Application, Uri ElasticsearchUri);

    private sealed class CatIndexRecord
    {
        public string Index { get; set; } = String.Empty;
    }
}
