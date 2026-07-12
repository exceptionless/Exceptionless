using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Exceptionless.Tests;

public class AppWebHostFactory : WebApplicationFactory<Startup>, IAsyncLifetime
{
    private const string SharedElasticsearchUrl = "http://localhost:9200";
    private static readonly string[] s_indexPrefixes = ["events", "migrations", "organizations", "projects", "saved-views", "stacks", "tokens", "users", "webhooks"];
    private static readonly string s_runScope = $"test-{Guid.NewGuid().ToString("N")[..8]}";
    private static int s_counter = -1;
    private static readonly Lazy<Task<DistributedApplication>> s_sharedAppHost = new(StartSharedAppHostAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly ConcurrentQueue<int> s_pool = new();
    private bool _sliceReleased;

    public AppWebHostFactory()
    {
        if (!s_pool.TryDequeue(out int instanceId))
            instanceId = Interlocked.Increment(ref s_counter);

        InstanceId = instanceId;
        AppScope = instanceId == 0 ? s_runScope : $"{s_runScope}-{instanceId}";
    }

    public string AppScope { get; }
    public int InstanceId { get; }
    public bool IndexesHaveBeenConfigured { get; set; }

    public async Task<string> GetRedisConnectionStringAsync(CancellationToken cancellationToken)
    {
        var app = await s_sharedAppHost.Value;
        return await app.GetConnectionStringAsync("Redis", cancellationToken)
            ?? throw new InvalidOperationException("Redis did not expose a connection string.");
    }

    public async ValueTask InitializeAsync()
    {
        _ = await s_sharedAppHost.Value;
        var elasticsearchUri = new Uri(SharedElasticsearchUrl);
        await WaitForElasticsearchAsync(elasticsearchUri);
        await CleanupElasticsearchSliceAsync(elasticsearchUri);
    }

    private static async Task<DistributedApplication> StartSharedAppHostAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Exceptionless_AppHost>(
            ["services-only", "--Logging:LogLevel:Default=Warning"],
            CancellationToken.None);
        var app = await appHost.BuildAsync(CancellationToken.None);
        await app.StartAsync(CancellationToken.None);

        return app;
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

        throw new TimeoutException("Timed out waiting for the shared Elasticsearch container to be ready.");
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
                ["AppScope"] = AppScope,
                ["ConnectionStrings:Elasticsearch"] = SharedElasticsearchUrl
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

    private sealed class CatIndexRecord
    {
        public string Index { get; set; } = String.Empty;
    }
}
