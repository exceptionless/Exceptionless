using System.Net;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class SpaHostingTests : IClassFixture<AppWebHostFactory>
{
    private readonly AppWebHostFactory _factory;

    public SpaHostingTests(AppWebHostFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/login")]
    [InlineData("/next/login")]
    [InlineData("/stack/507f1f77bcf86cd799439011/mark-fixed")]
    [InlineData("/project/507f1f77bcf86cd799439011/error/timeline")]
    [InlineData("/event/by-ref/order.123")]
    [InlineData("/next/event/by-ref/order.123")]
    public async Task GetAppRoute_ReturnsRootShell(string path)
    {
        using var client = _factory.CreateClient();
        string shell = await client.GetStringAsync("/index.html", TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

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
    public async Task GetMissingServiceOrAsset_DoesNotReturnShell(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostAppRoute_DoesNotReturnShell()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync("/login", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
