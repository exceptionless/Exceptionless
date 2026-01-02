using Exceptionless.Core.Authentication;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Helpers;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using FluentRest;
using FluentRest.NewtonsoftJson;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Storage;
using Foundatio.Utility;
using Foundatio.Xunit;
using Microsoft.AspNetCore.TestHost;
using Nest;
using Newtonsoft.Json;
using Xunit;
using HttpMethod = System.Net.Http.HttpMethod;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests;

public abstract class IntegrationTestsBase : TestWithLoggingBase, Xunit.IAsyncLifetime, IClassFixture<AppWebHostFactory>
{
    private static bool _indexesHaveBeenConfigured = false;
    private static readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private readonly ExceptionlessElasticConfiguration _configuration;
    protected readonly TestServer _server;
    private readonly ProxyTimeProvider _timeProvider;
    protected readonly IList<IDisposable> _disposables = new List<IDisposable>();

    public IntegrationTestsBase(ITestOutputHelper output, AppWebHostFactory factory) : base(output)
    {
        Log.DefaultLogLevel = LogLevel.Information;
        Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMessageBus>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryCacheClient>(LogLevel.Warning);
        Log.SetLogLevel("StartupActions", LogLevel.Warning);
        Log.SetLogLevel<Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager>(LogLevel.Warning);

        var configuredFactory = factory.Factories.Count > 0 ? factory.Factories[0] : null;
        if (configuredFactory is null)
        {
            configuredFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(RegisterServices); // happens after normal container configure and overrides services
            });
        }

        _server = configuredFactory.Server;
        _server.PreserveExecutionContext = true;

        var testScope = configuredFactory.Services.CreateScope();
        _disposables.Add(testScope);
        ServiceProvider = testScope.ServiceProvider;

        _configuration = GetService<ExceptionlessElasticConfiguration>();

        if (GetService<TimeProvider>() is ProxyTimeProvider proxyTimeProvider)
            _timeProvider = proxyTimeProvider;
        else
            throw new InvalidOperationException("TimeProvider must be of type ProxyTimeProvider");

        _disposables.Add(new DisposableAction(() => _timeProvider.Restore()));
    }

    public virtual async ValueTask InitializeAsync()
    {
        Log.SetLogLevel("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Warning);
        Log.SetLogLevel("Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService", LogLevel.None);
        await _server.WaitForReadyAsync();
        Log.SetLogLevel("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Information);
        Log.SetLogLevel("Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService", LogLevel.Information);

        await ResetDataAsync();
    }

    protected ProxyTimeProvider TimeProvider => _timeProvider;

    private IServiceProvider ServiceProvider { get; }

    protected TService GetService<TService>() where TService : notnull
    {
        return ServiceProvider.GetRequiredService<TService>();
    }

    protected virtual void RegisterServices(IServiceCollection services)
    {
        // use xunit test logger
        services.AddSingleton<ILoggerFactory>(Log);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.ReplaceSingleton<TimeProvider>(_ => new ProxyTimeProvider());

        services.AddSingleton<IMailer, NullMailer>();
        services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();

        services.AddSingleton<EventData>();
        services.AddTransient<EventDataBuilder>();
        services.AddSingleton<OrganizationData>();
        services.AddSingleton<ProjectData>();
        services.AddSingleton<RandomEventGenerator>();
        services.AddSingleton<StackData>();
        services.AddSingleton<TokenData>();
        services.AddSingleton<UserData>();

        services.ReplaceSingleton(s => _server.CreateHandler());
    }

    public async Task<(List<Stack> Stacks, List<PersistentEvent> Events)> CreateDataAsync(Action<DataBuilder> dataBuilderFunc)
    {
        var eventBuilders = new List<EventDataBuilder>();

        var dataBuilder = new DataBuilder(eventBuilders, ServiceProvider, _timeProvider);
        dataBuilderFunc(dataBuilder);

        var eventRepository = GetService<IEventRepository>();
        var stackRepository = GetService<IStackRepository>();

        var events = new HashSet<PersistentEvent>();
        var stacks = new HashSet<Stack>();

        foreach (var builder in eventBuilders)
        {
            var data = builder.Build();
            events.AddRange(data.Events);
            stacks.Add(data.Stack ?? throw new InvalidOperationException());
        }

        await stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());
        await eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        return (stacks.ToList(), events.ToList());
    }

    protected virtual async Task ResetDataAsync()
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            var oldLoggingLevel = Log.DefaultLogLevel;
            Log.DefaultLogLevel = LogLevel.Warning;

            await RefreshDataAsync();
            if (!_indexesHaveBeenConfigured)
            {
                await _configuration.DeleteIndexesAsync();
                await _configuration.ConfigureIndexesAsync();
                _indexesHaveBeenConfigured = true;
            }
            else
            {
                string indexes = String.Join(',', _configuration.Indexes.Select(i => i.Name));
                await _configuration.Client.DeleteByQueryAsync(new DeleteByQueryRequest(indexes)
                {
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

            Log.DefaultLogLevel = oldLoggingLevel;
        }
        finally
        {
            _semaphoreSlim.Release();
            _logger.LogDebug("Reset Data");
        }
    }

    protected async Task RefreshDataAsync(Indices? indices = null)
    {
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        var response = await configuration.Client.Indices.RefreshAsync(indices ?? Indices.All);
        _logger.LogRequest(response);
    }

    protected HttpClient CreateHttpClient()
    {
        var client = _server.CreateClient();
        client.BaseAddress = new Uri(_server.BaseAddress + "api/v2/", UriKind.Absolute);
        return client;
    }

    protected FluentClient CreateFluentClient()
    {
        var settings = GetService<JsonSerializerSettings>();
        return new FluentClient(CreateHttpClient(), new NewtonsoftJsonSerializer(settings));
    }

    protected async Task<HttpResponseMessage> SendRequestAsync(Action<AppSendBuilder> configure)
    {
        var client = CreateFluentClient();
        var request = new HttpRequestMessage(HttpMethod.Get, client.HttpClient.BaseAddress);
        var builder = new AppSendBuilder(request);
        configure(builder);

        var response = await client.SendAsync(request);

        var expectedStatus = request.GetExpectedStatus();
        if (expectedStatus.HasValue && expectedStatus.Value != response.StatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            if (content.Length > 1000)
                content = content.Substring(0, 1000);

            throw new HttpRequestException($"Expected status code {expectedStatus.Value} but received status code {response.StatusCode} ({response.ReasonPhrase}).\n" + content);
        }

        return response;
    }

    protected async Task<T?> SendRequestAsAsync<T>(Action<AppSendBuilder> configure)
    {
        var response = await SendRequestAsync(configure);

        // All errors are returned as problem details so if we are expecting Problem Details we shouldn't ensure success.
        bool ensureSuccess = !typeof(Microsoft.AspNetCore.Mvc.ProblemDetails).IsAssignableFrom(typeof(T));
        return await response.DeserializeAsync<T>(ensureSuccess);
    }

    protected Task<HttpResponseMessage> SendGlobalAdminRequestAsync(Action<AppSendBuilder> configure)
    {
        return SendRequestAsync(b =>
        {
            b.AsGlobalAdminUser();
            configure(b);
        });
    }

    protected async Task<T?> SendGlobalAdminRequestAsAsync<T>(Action<AppSendBuilder> configure)
    {
        var response = await SendGlobalAdminRequestAsync(configure);
        return await response.DeserializeAsync<T>();
    }

    protected Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        return response.DeserializeAsync<T>();
    }

    public virtual ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing resource");
            }
        }
        return ValueTask.CompletedTask;
    }
}
