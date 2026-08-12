using System.Net;
using System.Net.Http.Headers;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OAuthClientMetadataServiceTests
{
    [Theory]
    [InlineData("https://oauth.example/client.json", true)]
    [InlineData("https://oauth.example/clients/client.json", true)]
    [InlineData("https://oauth.example", false)]
    [InlineData("https://oauth.example/", false)]
    [InlineData("http://oauth.example/client.json", false)]
    [InlineData("https://oauth.example/client.json#fragment", false)]
    public void TryCreateClientMetadataDocumentUri_WithClientId_ValidatesHttpsPath(string clientId, bool expected)
    {
        // Arrange is provided by the theory data.

        // Act
        bool isValid = OAuthClientMetadataService.TryCreateClientMetadataDocumentUri(clientId, out _);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public async Task GetClientMetadataAsync_NoStoreResponse_DoesNotCacheDocument()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(CreateMetadataResponse);
        using var service = CreateService(handler);

        // Act
        var firstResult = await service.ClientMetadataService.GetClientMetadataAsync("https://oauth.example/client.json");
        var secondResult = await service.ClientMetadataService.GetClientMetadataAsync("https://oauth.example/client.json");

        // Assert
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(2, handler.RequestCount);

        static HttpResponseMessage CreateMetadataResponse()
        {
            var response = CreateSuccessfulMetadataResponse();
            response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            return response;
        }
    }

    [Fact]
    public async Task GetClientMetadataAsync_MaxAgeResponse_CachesDocument()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(CreateMetadataResponse);
        using var service = CreateService(handler);

        // Act
        var firstResult = await service.ClientMetadataService.GetClientMetadataAsync("https://oauth.example/client.json");
        var secondResult = await service.ClientMetadataService.GetClientMetadataAsync("https://oauth.example/client.json");

        // Assert
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(1, handler.RequestCount);

        static HttpResponseMessage CreateMetadataResponse()
        {
            var response = CreateSuccessfulMetadataResponse();
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };
            return response;
        }
    }

    [Fact]
    public async Task GetClientMetadataAsync_ZeroSharedMaxAgeResponse_DoesNotCacheDocument()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(CreateMetadataResponse);
        using var service = CreateService(handler);

        // Act
        var firstResult = await service.ClientMetadataService.GetClientMetadataAsync("https://oauth.example/client.json");
        var secondResult = await service.ClientMetadataService.GetClientMetadataAsync("https://oauth.example/client.json");

        // Assert
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(2, handler.RequestCount);

        static HttpResponseMessage CreateMetadataResponse()
        {
            var response = CreateSuccessfulMetadataResponse();
            response.Headers.CacheControl = new CacheControlHeaderValue
            {
                MaxAge = TimeSpan.FromHours(1),
                SharedMaxAge = TimeSpan.Zero
            };
            return response;
        }
    }

    private static OAuthClientMetadataServiceFixture CreateService(HttpMessageHandler handler)
    {
        var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = TimeProvider.System
        });
        var httpClient = new HttpClient(handler);
        var service = new OAuthClientMetadataService(
            httpClient,
            new OAuthServerOptions(),
            cache,
            NullLogger<OAuthClientMetadataService>.Instance,
            TimeProvider.System);

        return new OAuthClientMetadataServiceFixture(service, httpClient, cache);
    }

    private static HttpResponseMessage CreateSuccessfulMetadataResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "client_id": "https://oauth.example/client.json",
                  "client_name": "Example Client",
                  "redirect_uris": ["https://oauth.example/callback"]
                }
                """)
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory());
        }
    }

    private sealed record OAuthClientMetadataServiceFixture(
        OAuthClientMetadataService ClientMetadataService,
        HttpClient HttpClient,
        InMemoryCacheClient Cache) : IDisposable
    {
        public void Dispose()
        {
            HttpClient.Dispose();
            Cache.Dispose();
        }
    }
}
