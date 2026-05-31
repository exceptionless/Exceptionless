using System.Collections.Concurrent;
using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Configuration;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Foundatio.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests;

public class AppWebHostFactory : WebApplicationFactory<Exceptionless.Web.Program>, IAsyncLifetime
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

        var elasticsearch = builder.AddElasticsearch("Elasticsearch", port: 9200)
            .WithContainerName("Exceptionless-Elasticsearch-Test")
            .WithLifetime(ContainerLifetime.Persistent);

        var app = builder.Build();

        await app.StartAsync();

        var connectionString = await elasticsearch.Resource.GetConnectionStringAsync()
            ?? throw new InvalidOperationException("Could not resolve Elasticsearch connection string.");
        await WaitForElasticsearchAsync(new Uri(connectionString));

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

        throw new TimeoutException("Timed out waiting for Elasticsearch test container to be ready.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseDefaultServiceProvider(options =>
        {
            // Disable ValidateOnBuild because the service graph uses lambda factories
            // (queues, caching, Elasticsearch config) that resolve dependencies at runtime
            // through IServiceProvider, which cannot be statically validated at build time.
            options.ValidateOnBuild = false;
            options.ValidateScopes = true;
        });
        builder.UseSolutionRelativeContentRoot("src/Exceptionless.Web", "*.slnx");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: false)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AppScope"] = AppScope
                });
        });

        // In the minimal hosting model, Program.Main reads AppOptions BEFORE Build() applies
        // ConfigureAppConfiguration overrides. Re-register AppOptions from the final configuration
        // so the per-instance AppScope (test, test-1, test-2) is used correctly.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(sp =>
            {
                var config = (IConfigurationRoot)sp.GetRequiredService<IConfiguration>();
                var opts = AppOptions.ReadFromConfiguration(config);
                opts.QueueOptions.MetricsPollingEnabled = opts.RunJobsInProcess;
                return opts;
            });
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().CacheOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().MessageBusOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().QueueOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().StorageOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().EmailOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().ElasticsearchOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().IntercomOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().SlackOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().StripeOptions);
            services.AddSingleton(sp => sp.GetRequiredService<AppOptions>().AuthOptions);

            // Storage is registered before ConfigureAppConfiguration's AppScope override is applied.
            // Recreate it from the final test AppOptions so parallel test factories don't delete each
            // other's queued event payloads while ResetDataAsync clears scoped storage.
            services.ReplaceSingleton<IFileStorage>(CreateScopedFileStorage);
        });
    }

    private static IFileStorage CreateScopedFileStorage(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<AppOptions>().StorageOptions;
        IFileStorage storage;

        if (String.Equals(options.Provider, "folder", StringComparison.OrdinalIgnoreCase))
        {
            string path = options.Data.GetString("path", "|DataDirectory|\\storage");
            storage = new FolderFileStorage(new FolderFileStorageOptions
            {
                Folder = PathHelper.ExpandPath(path),
                Serializer = serviceProvider.GetRequiredService<ITextSerializer>(),
                TimeProvider = serviceProvider.GetRequiredService<TimeProvider>(),
                ResiliencePolicyProvider = serviceProvider.GetRequiredService<IResiliencePolicyProvider>(),
                LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
            });
        }
        else
        {
            storage = new InMemoryFileStorage(new InMemoryFileStorageOptions
            {
                Serializer = serviceProvider.GetRequiredService<ITextSerializer>(),
                TimeProvider = serviceProvider.GetRequiredService<TimeProvider>(),
                ResiliencePolicyProvider = serviceProvider.GetRequiredService<IResiliencePolicyProvider>(),
                LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>()
            });
        }

        return !String.IsNullOrWhiteSpace(options.Scope)
            ? new ScopedFileStorage(storage, options.Scope)
            : storage;
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
