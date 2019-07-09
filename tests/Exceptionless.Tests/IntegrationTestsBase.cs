using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using Exceptionless.Tests.Utility;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Tests.Authentication;
using FluentRest;
using Xunit.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Mail;
using Foundatio.Hosting.Startup;
using Foundatio.Logging.Xunit;
using Foundatio.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;
using Xunit;
using IAsyncLifetime = Xunit.IAsyncLifetime;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests {
    public class IntegrationTestsBase : TestWithLoggingBase, IAsyncLifetime, IClassFixture<AppWebHostFactory> {
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        protected readonly TestServer _server;
        protected readonly FluentClient _client;
        protected readonly HttpClient _httpClient;

        public IntegrationTestsBase(ITestOutputHelper output, AppWebHostFactory factory) : base(output) {
            Log.MinimumLevel = LogLevel.Information;
            Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);

            string currentDirectory = Directory.GetCurrentDirectory();
            var configuredFactory = factory.WithWebHostBuilder(builder => {
                builder.ConfigureAppConfiguration(config => config.SetBasePath(currentDirectory).AddYamlFile("appsettings.yml"));
                builder.ConfigureTestServices(RegisterServices); // happens after normal container configure and overrides services
            });
            
            _httpClient = configuredFactory.CreateClient();
            _server = configuredFactory.Server;
            _httpClient.BaseAddress = new Uri(_server.BaseAddress + "api/v2/");

            var settings = _server.Host.Services.GetRequiredService<JsonSerializerSettings>();
            _client = new FluentClient(_httpClient, new JsonContentSerializer(settings));
        }

        public virtual async Task InitializeAsync() {
            await _server.WaitForReadyAsync();
        }

        protected TService GetService<TService>() {
            return _server.Host.Services.GetRequiredService<TService>();
        }

        protected virtual void RegisterServices(IServiceCollection services) {
            services.AddSingleton<ILoggerFactory>(Log);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            services.AddSingleton<IMailer, NullMailer>();
            services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
            services.AddStartupAction("Delete indexes", DeleteIndexesAsync, 0);
        }

        protected async Task DeleteIndexesAsync(IServiceProvider serviceProvider) {
            var configuration = serviceProvider.GetRequiredService<ExceptionlessElasticConfiguration>();
            await configuration.DeleteIndexesAsync();
        }
        
        protected Task RefreshData(Indices indices = null) {
            var configuration = GetService<ExceptionlessElasticConfiguration>();
            return configuration.Client.RefreshAsync(indices ?? Indices.All);
        }
        
        protected async Task<HttpResponseMessage> SendRequest(Action<AppSendBuilder> configure) {
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

        protected async Task<T> SendRequestAs<T>(Action<AppSendBuilder> configure) {
            var response = await SendRequest(configure);
            return await response.DeserializeAsync<T>();
        }

        protected async Task<HttpResponseMessage> SendGlobalAdminRequest(Action<AppSendBuilder> configure) {
            return await SendRequest(b => {
                b.AsGlobalAdminUser();
                configure(b);
            });
        }

        protected async Task<T> SendGlobalAdminRequestAs<T>(Action<AppSendBuilder> configure) {
            var response = await SendGlobalAdminRequest(configure);
            return await response.DeserializeAsync<T>();
        }

        protected async Task<T> DeserializeResponse<T>(HttpResponseMessage response) {
            return await response.DeserializeAsync<T>();
        }

        public virtual Task DisposeAsync() {
            _testSystemClock.Dispose();
            _server?.Dispose();
            return Task.CompletedTask;
        }
    }
}