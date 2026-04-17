using System.Collections.Concurrent;
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
    private static int s_counter = -1;
    private static readonly ConcurrentQueue<int> s_pool = new();
    private static readonly Lazy<Task<DistributedApplication>> s_sharedApplication = new(StartSharedApplicationAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    private bool _sliceReleased;

    public AppWebHostFactory()
    {
        if (!s_pool.TryDequeue(out var instanceId))
            instanceId = Interlocked.Increment(ref s_counter);

        InstanceId = instanceId;
        AppScope = instanceId == 0 ? "test" : $"test-{instanceId}";
    }

    public string AppScope { get; }
    public int InstanceId { get; }
    public bool IndexesHaveBeenConfigured { get; set; }

    public async ValueTask InitializeAsync()
    {
        _ = await s_sharedApplication.Value;
    }

    private static async Task<DistributedApplication> StartSharedApplicationAsync()
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(ElasticsearchResource).Assembly.FullName, DisableDashboard = true };
        var builder = DistributedApplication.CreateBuilder(options);

        // don't use random ports for tests
        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";

        builder.AddElasticsearch("Elasticsearch", port: 9200)
            .WithContainerName("Exceptionless-Elasticsearch-Test")
            .WithLifetime(ContainerLifetime.Persistent);

        var app = builder.Build();

        await app.StartAsync();

        return app;
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

    public override ValueTask DisposeAsync()
    {
        if (!_sliceReleased)
        {
            s_pool.Enqueue(InstanceId);
            _sliceReleased = true;
        }

        return base.DisposeAsync();
    }
}
