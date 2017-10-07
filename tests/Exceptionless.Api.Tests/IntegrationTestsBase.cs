using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Json;
using FluentRest;
using Xunit.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.Api.Tests
{
    public class IntegrationTestsBase : TestBase {
        protected readonly ExceptionlessElasticConfiguration _configuration;
        protected readonly TestServer _server;
        protected readonly FluentClient _client;
        protected readonly JsonSerializer _serializer;

        public IntegrationTestsBase(ITestOutputHelper output) : base(output) {
            var builder = new MvcWebApplicationBuilder<Startup>()
                .UseSolutionRelativeContentRoot("src/Exceptionless.Api")
                .ConfigureAfterStartup(RegisterServices)
                .UseApplicationAssemblies();

            _server = builder.Build();

            var settings = GetService<Newtonsoft.Json.JsonSerializerSettings>();
            _serializer = GetService<JsonSerializer>();
            _client = new FluentClient(new JsonContentSerializer(settings), _server.CreateHandler()) {
                BaseUri = new Uri(_server.BaseAddress + "api/v2")
            };

            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _configuration.DeleteIndexesAsync().GetAwaiter().GetResult();
            _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false).GetAwaiter().GetResult();
        }

        protected override TService GetService<TService>() {
            return _server.Host.Services.GetRequiredService<TService>();
        }

        protected Task<FluentResponse> SendRequest(Action<SendBuilder> configure) {
            var request = _client.CreateRequest();
            var builder = new SendBuilder(request);
            configure(builder);

            if (request.ContentData != null && request.ContentData.GetType() != typeof(string)) {
                string content;
                using (var stringWriter = new StringWriter()) {
                    using (var jsonTextWriter = new JsonTextWriter(stringWriter)) {
                        _serializer.Serialize(jsonTextWriter, request.ContentData);
                        jsonTextWriter.Flush();
                        content = stringWriter.ToString();
                    }
                }
                request.ContentData = content;
            }

            return _client.SendAsync(request);
        }

        protected async Task<T> SendRequestAs<T>(Action<SendBuilder> configure) {
            var response = await SendRequest(configure);
            return await DeserializeResponse<T>(response);
        }

        protected Task<FluentResponse> SendTokenRequest(Token token, Action<SendBuilder> configure) {
            return SendTokenRequest(token.Id, configure);
        }

        protected async Task<FluentResponse> SendTokenRequest(string token, Action<SendBuilder> configure) {
            return await SendRequest(s => {
                s.BearerToken(token);
                configure(s);
            });
        }

        protected async Task<T> SendTokenRequestAs<T>(string token, Action<SendBuilder> configure) {
            var response = await SendTokenRequest(token, configure);
            return await DeserializeResponse<T>(response);
        }

        protected async Task<FluentResponse> SendUserRequest(string username, string password, Action<SendBuilder> configure) {
            return await SendRequest(s => {
                s.BasicAuthorization(username, password);
                configure(s);
            });
        }

        protected async Task<T> SendUserRequestAs<T>(string username, string password, Action<SendBuilder> configure) {
            var response = await SendUserRequest(username, password, configure);
            return await DeserializeResponse<T>(response);
        }

        protected async Task<T> DeserializeResponse<T>(FluentResponse response) {
            string json = await response.HttpContent.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

            var reader = new JsonTextReader(new StringReader(json));
            var result = _serializer.Deserialize<T>(reader);

            return result;
        }

        public override void Dispose() {
            _server?.Dispose();
            _configuration.Dispose();
            base.Dispose();
        }
    }
}