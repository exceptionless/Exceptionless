using Exceptionless.Core.Authentication;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Tests.Utility;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Authentication;
using FluentRest;
using Xunit.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Mail;
using FluentRest.NewtonsoftJson;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Xunit;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Storage;
using Foundatio.Utility;
using Nest;
using Newtonsoft.Json;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;

namespace Exceptionless.Tests;

public abstract class IntegrationTestsBase : TestWithLoggingBase, Xunit.IAsyncLifetime, IClassFixture<AppWebHostFactory> {
    private static bool _indexesHaveBeenConfigured = false;
    private static readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private readonly IDisposable _testSystemClock = TestSystemClock.Install();
    private readonly ExceptionlessElasticConfiguration _configuration;
    protected readonly TestServer _server;
    protected readonly IList<IDisposable> _disposables = new List<IDisposable>();

    public IntegrationTestsBase(ITestOutputHelper output, AppWebHostFactory factory) : base(output) {
        Log.MinimumLevel = LogLevel.Information;
        Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMessageBus>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryCacheClient>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMetricsClient>(LogLevel.Information);
        Log.SetLogLevel("StartupActions", LogLevel.Warning);
        Log.SetLogLevel<Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager>(LogLevel.Warning);

        var configuredFactory = factory.Factories.Count > 0 ? factory.Factories[0] : null;
        if (configuredFactory == null) {
            configuredFactory = factory.WithWebHostBuilder(builder => {
                builder.ConfigureTestServices(RegisterServices); // happens after normal container configure and overrides services
            });
        }

        _disposables.Add(_testSystemClock);

        _server = configuredFactory.Server;
        _server.PreserveExecutionContext = true;

        var testScope = configuredFactory.Services.CreateScope();
        _disposables.Add(testScope);
        ServiceProvider = testScope.ServiceProvider;

        _configuration = GetService<ExceptionlessElasticConfiguration>();
    }

    public virtual async Task InitializeAsync() {
        Log.SetLogLevel("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Warning);
        Log.SetLogLevel("Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService", LogLevel.None);
        await _server.WaitForReadyAsync();
        Log.SetLogLevel("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Information);
        Log.SetLogLevel("Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService", LogLevel.Information);

        await ResetDataAsync();
    }

    private IServiceProvider ServiceProvider { get; }

    protected TService GetService<TService>() {
        return ServiceProvider.GetRequiredService<TService>();
    }

    protected virtual void RegisterServices(IServiceCollection services) {
        // use xunit test logger
        services.AddSingleton<ILoggerFactory>(Log);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        services.AddSingleton<IMailer, NullMailer>();
        services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();

        services.AddTransient<EventDataBuilder>();

        services.ReplaceSingleton(s => _server.CreateHandler());
    }

    public async Task<(List<Stack> Stacks, List<PersistentEvent> Events)> CreateDataAsync(Action<DataBuilder> dataBuilderFunc) {
        var eventBuilders = new List<EventDataBuilder>();

        var dataBuilder = new DataBuilder(eventBuilders, ServiceProvider);
        dataBuilderFunc(dataBuilder);

        var eventRepository = GetService<IEventRepository>();
        var stackRepository = GetService<IStackRepository>();

        var events = new HashSet<PersistentEvent>();
        var stacks = new HashSet<Stack>();

        foreach (var builder in eventBuilders) {
            var data = builder.Build();
            events.AddRange(data.Events);
            stacks.Add(data.Stack);
        }

        await stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());
        await eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        return (stacks.ToList(), events.ToList());
    }

    protected virtual async Task ResetDataAsync() {
        await _semaphoreSlim.WaitAsync();
        try {
            var oldLoggingLevel = Log.MinimumLevel;
            Log.MinimumLevel = LogLevel.Warning;

            await RefreshDataAsync();
            if (!_indexesHaveBeenConfigured) {
                await _configuration.DeleteIndexesAsync();
                await _configuration.ConfigureIndexesAsync();
                _indexesHaveBeenConfigured = true;
            }
            else {
                string indexes = String.Join(',', _configuration.Indexes.Select(i => i.Name));
                await _configuration.Client.DeleteByQueryAsync(new DeleteByQueryRequest(indexes) {
                    Query = new MatchAllQuery(),
                    IgnoreUnavailable = true,
                    Refresh = true
                });
            }

            _logger.LogTrace("Configured Indexes");

            foreach (var index in _configuration.Indexes)
                index.QueryParser.Configuration.MappingResolver.RefreshMapping();

            var cacheClient = GetService<ICacheClient>();
            await cacheClient.RemoveAllAsync();

            var fileStorage = GetService<IFileStorage>();
            await fileStorage.DeleteFilesAsync(await fileStorage.GetFileListAsync());

            await GetService<IQueue<WorkItemData>>().DeleteQueueAsync();

            Log.MinimumLevel = oldLoggingLevel;
        }
        finally {
            _semaphoreSlim.Release();
            _logger.LogDebug("Reset Data");
        }
    }

    protected async Task RefreshDataAsync(Indices indices = null) {
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        var response = await configuration.Client.Indices.RefreshAsync(indices ?? Indices.All);
        _logger.LogRequest(response);
    }

    protected HttpClient CreateHttpClient() {
        var client = _server.CreateClient();
        client.BaseAddress = new Uri(_server.BaseAddress + "api/v2/", UriKind.Absolute);
        return client;
    }

    protected FluentClient CreateFluentClient() {
        var settings = GetService<JsonSerializerSettings>();
        return new FluentClient(CreateHttpClient(), new NewtonsoftJsonSerializer(settings));
    }

    protected async Task<HttpResponseMessage> SendRequestAsync(Action<AppSendBuilder> configure) {
        var client = CreateFluentClient();
        var request = new HttpRequestMessage(HttpMethod.Get, client.HttpClient.BaseAddress);
        var builder = new AppSendBuilder(request);
        configure(builder);

        var response = await client.SendAsync(request);

        var expectedStatus = request.GetExpectedStatus();
        if (expectedStatus.HasValue && expectedStatus.Value != response.StatusCode) {
            string content = await response.Content.ReadAsStringAsync();
            if (content.Length > 1000)
                content = content.Substring(0, 1000);

            throw new HttpRequestException($"Expected status code {expectedStatus.Value} but received status code {response.StatusCode} ({response.ReasonPhrase}).\n" + content);
        }

        return response;
    }

    protected async Task<T> SendRequestAsAsync<T>(Action<AppSendBuilder> configure) {
        var response = await SendRequestAsync(configure);
        return await response.DeserializeAsync<T>();
    }

    protected Task<HttpResponseMessage> SendGlobalAdminRequestAsync(Action<AppSendBuilder> configure) {
        return SendRequestAsync(b => {
            b.AsGlobalAdminUser();
            configure(b);
        });
    }

    protected async Task<T> SendGlobalAdminRequestAsAsync<T>(Action<AppSendBuilder> configure) {
        var response = await SendGlobalAdminRequestAsync(configure);
        return await response.DeserializeAsync<T>();
    }

    protected Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response) {
        return response.DeserializeAsync<T>();
    }

    public virtual Task DisposeAsync() {
        foreach (var disposable in _disposables) {
            try {
                disposable.Dispose();
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "Error disposing resource.");
            }
        }
        return Task.CompletedTask;
    }
}
