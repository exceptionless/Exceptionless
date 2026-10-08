using System.Net;
using Exceptionless.Tests.Extensions;
using Foundatio.Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class SpaHostingTests : TestWithLoggingBase, IClassFixture<AppWebHostFactory>
{
    private readonly AppWebHostFactory _factory;

    public SpaHostingTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output) => _factory = factory;

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
    public async Task GetAsync_ConfiguredApiOrigin_UsesRestrictedConnectionsAndStrictScripts()
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
        Assert.DoesNotContain("*", sources);
        Assert.DoesNotContain("ws:", sources);
        Assert.DoesNotContain("wss:", sources);
        Assert.Contains("https://localhost:9443", sources);
        Assert.Contains("wss://localhost:9443", sources);
        Assert.DoesNotContain(apiUrl, sources);

        // The published static shell relies on this response header for deny-by-default and
        // anti-framing protection; frame-ancestors cannot be enforced by its meta policy.
        string defaults = Assert.Single(policy.Split(';'), directive => directive.StartsWith("default-src ", StringComparison.Ordinal));
        Assert.Equal(["default-src", "'none'"], defaults.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string ancestors = Assert.Single(policy.Split(';'), directive => directive.StartsWith("frame-ancestors ", StringComparison.Ordinal));
        Assert.Equal(["frame-ancestors", "'none'"], ancestors.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));

        string frames = Assert.Single(policy.Split(';'), directive => directive.StartsWith("frame-src ", StringComparison.Ordinal));
        Assert.DoesNotContain("'self'", frames.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string media = Assert.Single(policy.Split(';'), directive => directive.StartsWith("media-src ", StringComparison.Ordinal));
        Assert.DoesNotContain("'self'", media.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string workers = Assert.Single(policy.Split(';'), directive => directive.StartsWith("worker-src ", StringComparison.Ordinal));
        Assert.Equal(["worker-src", "'none'"], workers.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain(policy.Split(';'), directive => directive.StartsWith("manifest-src ", StringComparison.Ordinal));

        // Connection configuration must not weaken script execution restrictions.
        string scripts = Assert.Single(policy.Split(';'), directive => directive.StartsWith("script-src ", StringComparison.Ordinal));
        string[] scriptSources = scripts.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain("'unsafe-inline'", scriptSources);
        Assert.DoesNotContain("'unsafe-eval'", scriptSources);
        Assert.Contains("'strict-dynamic'", scriptSources);
        Assert.Contains("https://*.stripe.com", scriptSources);
        Assert.Contains("https://*.intercom.io", scriptSources);
    }
}
