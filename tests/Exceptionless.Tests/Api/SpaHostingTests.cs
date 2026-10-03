using System.Net;
using Exceptionless.Tests.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class SpaHostingTests : IClassFixture<AppWebHostFactory>
{
    private readonly AppWebHostFactory _factory;

    public SpaHostingTests(AppWebHostFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/login")]
    [InlineData("/stack/507f1f77bcf86cd799439011/mark-fixed")]
    [InlineData("/project/507f1f77bcf86cd799439011/error/timeline")]
    [InlineData("/event/by-ref/order.123")]
    public async Task GetAsync_ApplicationRoute_ReturnsRootShell(string path)
    {
        // Arrange
        await _factory.Server.WaitForReadyAsync();
        using var client = _factory.CreateClient();
        string shell = await client.GetStringAsync("/index.html", TestContext.Current.CancellationToken);

        // Act
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(shell, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/docs/missing/nested")]
    [InlineData("/health/missing")]
    [InlineData("/ready/missing")]
    [InlineData("/.well-known/missing")]
    [InlineData("/_app/missing")]
    [InlineData("/_app/missing.js")]
    [InlineData("/missing.css")]
    public async Task GetAsync_MissingServiceOrAsset_DoesNotReturnShell(string path)
    {
        // Arrange
        await _factory.Server.WaitForReadyAsync();
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostAsync_ApplicationRoute_DoesNotReturnShell()
    {
        // Arrange
        await _factory.Server.WaitForReadyAsync();
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.PostAsync("/login", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetAsync_ConfiguredApiOrigin_AllowsApiAndWebSocketConnections()
    {
        // Arrange
        const string apiUrl = "https://localhost:9443/backend?ignored=true";
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiUrl"] = apiUrl })));
        await factory.Server.WaitForReadyAsync();
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/login", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        string connections = Assert.Single(policy.Split(';'), directive => directive.StartsWith("connect-src ", StringComparison.Ordinal));
        string[] sources = connections.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("'self'", sources);
        Assert.Contains("https://localhost:9443", sources);
        Assert.Contains("wss://localhost:9443", sources);
        Assert.DoesNotContain("*", sources);
        Assert.DoesNotContain(apiUrl, sources);

        // API-origin validation must preserve the existing script compatibility policy.
        string scripts = Assert.Single(policy.Split(';'), directive => directive.StartsWith("script-src ", StringComparison.Ordinal));
        string[] scriptSources = scripts.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("'unsafe-inline'", scriptSources);
        Assert.Contains("'unsafe-eval'", scriptSources);
        Assert.Contains("https://js.stripe.com", scriptSources);
        Assert.Contains("https://widget.intercom.io", scriptSources);
    }
}
