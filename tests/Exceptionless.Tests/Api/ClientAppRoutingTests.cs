using System.Net;
using Exceptionless.Web.Extensions;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class ClientAppRoutingTests : IAsyncLifetime
{
    private readonly string _webRoot = Path.Join(Path.GetTempPath(), $"exceptionless-spa-{Guid.NewGuid():N}");
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Join(_webRoot, "_app"));
        Directory.CreateDirectory(Path.Join(_webRoot, "img"));
        await File.WriteAllTextAsync(Path.Join(_webRoot, "index.html"), "<html>Svelte application</html>");
        await File.WriteAllTextAsync(Path.Join(_webRoot, "_app", "env.js"), "export const env={};");
        await File.WriteAllTextAsync(Path.Join(_webRoot, "img", "exceptionless-logo.png"), "email logo");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _webRoot, WebRootPath = _webRoot, EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        _app = builder.Build();
        _app.UseClientAppRedirects();
        _app.UseStaticFiles();
        _app.MapGet("/api/v2/about", () => Microsoft.AspNetCore.Http.Results.Json(new { name = "API" }));
        _app.MapGet("/docs", () => Microsoft.AspNetCore.Http.Results.Text("API documentation"));
        _app.MapGet("/health", () => Microsoft.AspNetCore.Http.Results.Ok());
        _app.MapClientAppFallback();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    [Theory]
    [InlineData("/next", "/")]
    [InlineData("/next/", "/")]
    [InlineData("/NEXT/login?redirect=%2Fstack%2F123", "/login?redirect=%2Fstack%2F123")]
    [InlineData("/next/event/by-ref/reference.123?project=123", "/event/by-ref/reference.123?project=123")]
    [InlineData("/next//example.com/path", "/example.com/path")]
    [InlineData("/next/api/v2/about", "/api/v2/about")]
    public async Task Get_LegacyPrefix_RedirectsToLocalRoot(string path, string destination)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal(destination, response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/oauth/authorize?client_id=test")]
    [InlineData("/account/verify?token=test")]
    [InlineData("/stack/123/event/456?action=fixed")]
    [InlineData("/event/by-ref/reference.123")]
    [InlineData("/nextdoor")]
    public async Task Get_AppDeepLink_ServesSvelteShell(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Svelte application", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/api/v2/missing")]
    [InlineData("/API/missing")]
    [InlineData("/docs/missing")]
    [InlineData("/mcp/missing")]
    [InlineData("/.well-known/missing")]
    [InlineData("/health/missing")]
    [InlineData("/ready/missing")]
    [InlineData("/_app/missing")]
    [InlineData("/img/missing.png")]
    public async Task Get_UnknownServiceOrAsset_DoesNotServeShell(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Svelte application", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Get_ExistingServicesAndAssets_RetainsTheirContent()
    {
        Assert.Contains("API", await _client.GetStringAsync("/api/v2/about", TestContext.Current.CancellationToken));
        Assert.Equal("API documentation", await _client.GetStringAsync("/docs", TestContext.Current.CancellationToken));
        Assert.Equal("export const env={};", await _client.GetStringAsync("/_app/env.js", TestContext.Current.CancellationToken));
        Assert.Equal("email logo", await _client.GetStringAsync("/img/exceptionless-logo.png", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task NonGet_UnknownAppPath_DoesNotServeShell(string method)
    {
        using var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/stack/123"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Svelte application", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Head_AppDeepLink_ReturnsHtmlHeadersWithoutBody()
    {
        using var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/stack/123"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }
}
