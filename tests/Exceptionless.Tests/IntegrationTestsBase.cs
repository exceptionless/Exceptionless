using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Tests.Utility;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Authentication;
using FluentRest;
using Xunit.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Mail;
using FluentRest.NewtonsoftJson;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging.Xunit;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using Xunit;
using IAsyncLifetime = Xunit.IAsyncLifetime;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests {
    public abstract class IntegrationTestsBase : TestWithLoggingBase, IAsyncLifetime, IClassFixture<AppWebHostFactory> {
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        private readonly ExceptionlessElasticConfiguration _configuration;
        protected readonly IList<IDisposable> _disposables = new List<IDisposable>();
        protected readonly TestServer _server;
        protected readonly FluentClient _client;
        protected readonly HttpClient _httpClient;

        public IntegrationTestsBase(ITestOutputHelper output, AppWebHostFactory factory) : base(output) {
            Log.MinimumLevel = LogLevel.Information;
            Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
            Log.SetLogLevel<InMemoryMessageBus>(LogLevel.Warning);
            Log.SetLogLevel<InMemoryCacheClient>(LogLevel.Warning);
            Log.SetLogLevel<InMemoryMetricsClient>(LogLevel.Information);
            Log.SetLogLevel<Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager>(LogLevel.Warning);

            var configuredFactory = factory.Factories.FirstOrDefault();
            if (configuredFactory == null) {
                configuredFactory = factory.WithWebHostBuilder(builder => {
                   builder.ConfigureTestServices(RegisterServices); // happens after normal container configure and overrides services
               });
            }

            _disposables.Add(_testSystemClock);
            
            _httpClient = configuredFactory.CreateClient();
            _server = configuredFactory.Server;
            _httpClient.BaseAddress = new Uri(_server.BaseAddress + "api/v2/", UriKind.Absolute);

            var testScope = configuredFactory.Services.CreateScope();
            _disposables.Add(testScope);
            ServiceProvider = testScope.ServiceProvider;
            
            var settings = GetService<JsonSerializerSettings>();
            _client = new FluentClient(_httpClient, new NewtonsoftJsonSerializer(settings));
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
            
            services.ReplaceSingleton(s => _server.CreateHandler());
        }

        protected virtual async Task ResetDataAsync() {
            await _semaphoreSlim.WaitAsync();
            try {
                var oldLoggingLevel = Log.MinimumLevel;
                Log.MinimumLevel = LogLevel.Warning;

                bool isTraceLogLevelEnabled = _logger.IsEnabled(LogLevel.Trace);
                
                await _configuration.DeleteIndexesAsync();
                await _configuration.ConfigureIndexesAsync();
                
                if (isTraceLogLevelEnabled)
                    _logger.LogTrace("Configured Indexes");
                
                foreach (var index in _configuration.Indexes)
                    index.QueryParser.Configuration.RefreshMapping();

                var cacheClient = GetService<ICacheClient>();
                await cacheClient.RemoveAllAsync();

                var fileStorage = GetService<IFileStorage>();
                await fileStorage.DeleteFilesAsync(await fileStorage.GetFileListAsync());

                await GetService<IQueue<WorkItemData>>().DeleteQueueAsync();

                Log.MinimumLevel = oldLoggingLevel;
            } finally {
                _semaphoreSlim.Release();
            }
        }
        
        protected Task RefreshDataAsync(Indices indices = null) {
            var configuration = GetService<ExceptionlessElasticConfiguration>();
            return configuration.Client.Indices.RefreshAsync(indices ?? Indices.All);
        }
        
        protected async Task<HttpResponseMessage> SendRequestAsync(Action<AppSendBuilder> configure) {
            var request = new HttpRequestMessage(HttpMethod.Get, _client.HttpClient.BaseAddress);
            var builder = new AppSendBuilder(request);
            configure(builder);

            var response = await _client.SendAsync(request);

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

        protected async Task<HttpResponseMessage> SendGlobalAdminRequestAsync(Action<AppSendBuilder> configure) {
            return await SendRequestAsync(b => {
                b.AsGlobalAdminUser();
                configure(b);
            });
        }

        protected async Task<T> SendGlobalAdminRequestAsAsync<T>(Action<AppSendBuilder> configure) {
            var response = await SendGlobalAdminRequestAsync(configure);
            return await response.DeserializeAsync<T>();
        }

        protected async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response) {
            return await response.DeserializeAsync<T>();
        }

        public virtual Task DisposeAsync() {
            foreach (var disposable in _disposables) {
                try {
                    disposable.Dispose();
                } catch (Exception ex) {
                    _logger?.LogError(ex, "Error disposing resource.");
                }
            }
            return Task.CompletedTask;
        }
    }
}