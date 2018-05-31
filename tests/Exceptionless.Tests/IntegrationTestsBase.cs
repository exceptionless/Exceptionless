using System;
using System.Net.Http;
using System.Threading.Tasks;
using Exceptionless.Tests.Utility;
using Exceptionless.Core.Repositories.Configuration;
using FluentRest;
using Foundatio.Serializer;
using Xunit.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Exceptionless.Web;
using Newtonsoft.Json;
using Exceptionless.Tests.Extensions;

namespace Exceptionless.Tests {
    public class IntegrationTestsBase : TestBase {
        protected readonly ExceptionlessElasticConfiguration _configuration;
        protected readonly TestServer _server;
        protected readonly FluentClient _client;
        protected readonly HttpClient _httpClient;
        protected readonly ISerializer _serializer;

        public IntegrationTestsBase(ITestOutputHelper output) : base(output) {
            var builder = new MvcWebApplicationBuilder<Startup>()
                .UseSolutionRelativeContentRoot("src/Exceptionless.Web")
                .ConfigureBeforeStartup(Configure)
                .ConfigureAfterStartup(RegisterServices)
                .UseApplicationAssemblies();

            _server = builder.Build();

            var settings = GetService<JsonSerializerSettings>();
            _serializer = GetService<ITextSerializer>();
            _httpClient = new HttpClient(_server.CreateHandler()) {
                BaseAddress = new Uri(_server.BaseAddress + "api/v2/")
            };
            _client = new FluentClient(_httpClient, new JsonContentSerializer(settings));

            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _configuration.DeleteIndexesAsync().GetAwaiter().GetResult();
            _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false).GetAwaiter().GetResult();
        }

        protected override TService GetService<TService>() {
            return _server.Host.Services.GetRequiredService<TService>();
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

        public override void Dispose() {
            _server?.Dispose();
            _configuration.Dispose();
            base.Dispose();
        }
    }
}