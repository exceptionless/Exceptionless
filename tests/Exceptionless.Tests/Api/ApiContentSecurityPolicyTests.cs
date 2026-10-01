using Exceptionless.Tests.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class ApiContentSecurityPolicyTests(AppWebHostFactory factory) : IClassFixture<AppWebHostFactory>
{
    [Theory]
    [InlineData(" https://api.localhost:9443/backend?ignored=true#ignored ", "https://api.localhost:9443", "wss://api.localhost:9443")]
    [InlineData("http://api.localhost:8111/backend", "http://api.localhost:8111", "ws://api.localhost:8111")]
    [InlineData("HTTPS://API.LOCALHOST:443/backend", "https://api.localhost", "wss://api.localhost")]
    [InlineData("http://[::1]:8111/backend", "http://[::1]:8111", "ws://[::1]:8111")]
    public async Task GetDocs_ConfiguredApi_AllowsOnlyHttpAndWebSocketOrigins(string apiUrl, string httpOrigin, string webSocketOrigin)
    {
        await using var configuredFactory = CreateConfiguredFactory(apiUrl);
        string[] sources = await GetConnectionSourcesAsync(configuredFactory);

        Assert.Contains("'self'", sources);
        Assert.Contains(httpOrigin, sources);
        Assert.Contains(webSocketOrigin, sources);
        Assert.DoesNotContain("*", sources);
        Assert.DoesNotContain(apiUrl, sources);
        Assert.DoesNotContain(sources, source => source.Contains("untrusted.localhost", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/api")]
    [InlineData("//api.localhost")]
    [InlineData("ftp://api.localhost")]
    [InlineData("wss://api.localhost")]
    [InlineData("https://user:password@api.localhost")]
    [InlineData("https://*.example.test")]
    [InlineData("https://api.localhost;script-src")]
    public async Task GetDocs_InvalidApiConfiguration_DoesNotExpandConnectionPolicy(string? apiUrl)
    {
        await using var baselineFactory = CreateConfiguredFactory(null);
        string[] baseline = await GetConnectionSourcesAsync(baselineFactory);
        await using var configuredFactory = CreateConfiguredFactory(apiUrl);

        string[] sources = await GetConnectionSourcesAsync(configuredFactory);

        Assert.Equal(baseline, sources);
    }

    private WebApplicationFactory<Web.Program> CreateConfiguredFactory(string? apiUrl)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiUrl"] = apiUrl })));
    }

    private static async Task<string[]> GetConnectionSourcesAsync(WebApplicationFactory<Web.Program> configuredFactory)
    {
        await configuredFactory.Server.WaitForReadyAsync();
        using var client = configuredFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/docs/v2");
        request.Headers.Host = "untrusted.localhost:12345";
        request.Headers.Add("X-Forwarded-Host", "untrusted.localhost:12345");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        string policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        string connections = Assert.Single(policy.Split(';', StringSplitOptions.TrimEntries), directive => directive.StartsWith("connect-src ", StringComparison.Ordinal));
        return connections.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
    }
}
