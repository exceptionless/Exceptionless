using System.Collections.Concurrent;
using System.Net;
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
    private static int s_counter = -1;
    private static readonly string s_processScope = $"testprocess-{Environment.ProcessId}";
    private static readonly Lazy<Task<DistributedApplication>> s_sharedAppHost = new(StartSharedAppHostAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly ConcurrentQueue<int> s_pool = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_dataResetLocks = new();
    private static readonly ConcurrentDictionary<string, byte> s_configuredIndexes = new();
    private bool _sliceReleased;

    public AppWebHostFactory()
    {
        if (!s_pool.TryDequeue(out int instanceId))
            instanceId = Interlocked.Increment(ref s_counter);

        InstanceId = instanceId;
        AppScope = instanceId == 0 ? s_processScope : $"{s_processScope}-{instanceId}";
    }

    public string AppScope { get; }
    public int InstanceId { get; }
    public SemaphoreSlim DataResetLock => s_dataResetLocks.GetOrAdd(AppScope, _ => new SemaphoreSlim(1, 1));
    public bool IndexesHaveBeenConfigured
    {
        get => s_configuredIndexes.ContainsKey(AppScope);
        set
        {
            if (value)
                s_configuredIndexes.TryAdd(AppScope, 0);
            else
                s_configuredIndexes.TryRemove(AppScope, out _);
        }
    }

    public async ValueTask InitializeAsync()
    {
        _ = await s_sharedAppHost.Value;
        await WaitForElasticsearchAsync(new Uri(SharedElasticsearchUrl));
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
        await base.DisposeAsync();

        if (!_sliceReleased)
        {
            s_pool.Enqueue(InstanceId);
            _sliceReleased = true;
        }
    }
}
