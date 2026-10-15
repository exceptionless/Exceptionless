using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Exceptionless.Tests.Extensions;
using Foundatio.Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
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
        string shell = await client.GetStringAsync("/index.html", TestCancellationToken);

        // Act
        using var response = await client.GetAsync(path, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal(shell, await response.Content.ReadAsStringAsync(TestCancellationToken));
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
        using var response = await client.GetAsync(path, TestCancellationToken);

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
        using var response = await client.PostAsync("/login", null, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_ConfiguredApiOrigin_UsesRestrictedConnectionsAndStrictScripts(bool enableSsl)
    {
        // Arrange
        const string siteUrl = "https://site.localhost:8111";
        const string apiUrl = "https://localhost:9443/backend?ignored=true";
        const string telemetryUrl = "https://telemetry.localhost:9555/collector?ignored=true";
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = siteUrl,
                ["ApiUrl"] = apiUrl,
                ["ExceptionlessServerUrl"] = telemetryUrl,
                ["EnableSsl"] = enableSsl.ToString()
            })));
        await factory.Server.WaitForReadyAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "poisoned.localhost:4444";
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "forwarded.localhost:5555");
        client.DefaultRequestHeaders.Add("Forwarded", "host=forwarded.localhost:5555;proto=http");

        // Act
        using var response = await client.GetAsync("/login", TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal(enableSsl, policy.Split(';', StringSplitOptions.TrimEntries).Contains("upgrade-insecure-requests"));
        string connections = Assert.Single(policy.Split(';'), directive => directive.StartsWith("connect-src ", StringComparison.Ordinal));
        string[] sources = connections.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain("*", sources);
        Assert.DoesNotContain("ws:", sources);
        Assert.DoesNotContain("wss:", sources);
        Assert.Equal([
            "connect-src", "'self'", "https://*.exceptionless.io", "https://api.stripe.com",
            "https://link.com", "https://*.link.com", "https://*.intercom.io", "wss://*.intercom.io",
            "https://*.intercom-messenger.com", "wss://*.intercom-messenger.com",
            siteUrl, "wss://site.localhost:8111", "https://localhost:9443", "wss://localhost:9443", "https://telemetry.localhost:9555"
        ], sources);

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

    [Fact]
    public async Task GetAsync_PublishedSpa_TrustsStartupHashWithoutRewritingResponses()
    {
        // Arrange
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        const string bootstrap = "import(\"/_app/env.js?v=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\");start()";
        string hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(bootstrap)));
        string html = $"""<meta http-equiv="Content-Security-Policy" content="script-src 'strict-dynamic' 'sha256-{hash}'"><script>{bootstrap}</script>""";
        string injectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("injected()")));
        string changedHtml = html + "<script>injected()</script>";

        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), html, TestCancellationToken);
            string envPath = Path.Combine(webRoot, "_app", "env.js");
            Directory.CreateDirectory(Path.GetDirectoryName(envPath)!);
            await File.WriteAllTextAsync(envPath, "export const env={version:1};", TestCancellationToken);
            using var files = new PhysicalFileProvider(webRoot);
            // Use the published fixture directly, without the development asset manifest overlay.
            await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices((context, _) =>
                context.HostingEnvironment.WebRootFileProvider = files));
            await factory.Server.WaitForReadyAsync();
            using var client = factory.CreateClient();
            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), changedHtml, TestCancellationToken);

            // Act
            using var response = await client.GetAsync("/index.html", TestCancellationToken);
            string body = await response.Content.ReadAsStringAsync(TestCancellationToken);
            string policy = response.Headers.GetValues("Content-Security-Policy").Single();
            using var firstEnvironment = await client.GetAsync("/_app/env.js?v=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", TestCancellationToken);
            string firstEnvironmentBody = await firstEnvironment.Content.ReadAsStringAsync(TestCancellationToken);
            await File.WriteAllTextAsync(envPath, "export const env={version:2};", TestCancellationToken);
            using var secondEnvironment = await client.GetAsync("/_app/env.js?v=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", TestCancellationToken);
            string secondEnvironmentBody = await secondEnvironment.Content.ReadAsStringAsync(TestCancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(changedHtml, body);
            Assert.Contains("default-src 'none'", policy);
            Assert.Contains($"'sha256-{hash}'", policy);
            Assert.DoesNotContain(injectedHash, policy);
            Assert.DoesNotContain("nonce=", body);
            Assert.Equal(HttpStatusCode.OK, firstEnvironment.StatusCode);
            Assert.Equal(HttpStatusCode.OK, secondEnvironment.StatusCode);
            Assert.Null(firstEnvironment.Headers.CacheControl);
            Assert.Null(secondEnvironment.Headers.CacheControl);
            Assert.Equal("export const env={version:1};", firstEnvironmentBody);
            Assert.Equal("export const env={version:2};", secondEnvironmentBody);
            Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_Scalar_UsesFreshMatchingNonces()
    {
        // Arrange
        await _factory.Server.WaitForReadyAsync();
        using var client = _factory.CreateClient();

        // Act
        using var firstResponse = await client.GetAsync("/docs/", TestCancellationToken);
        using var secondResponse = await client.GetAsync("/docs/", TestCancellationToken);
        string firstBody = await firstResponse.Content.ReadAsStringAsync(TestCancellationToken);
        string secondBody = await secondResponse.Content.ReadAsStringAsync(TestCancellationToken);
        MatchCollection firstNonces = Regex.Matches(firstBody, "<script\\b[^>]*\\bnonce=\"(?<nonce>[^\"]+)\"");
        MatchCollection secondNonces = Regex.Matches(secondBody, "<script\\b[^>]*\\bnonce=\"(?<nonce>[^\"]+)\"");

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(3, firstNonces.Count);
        Assert.Equal(3, secondNonces.Count);
        string firstNonce = WebUtility.HtmlDecode(firstNonces[0].Groups["nonce"].Value);
        string secondNonce = WebUtility.HtmlDecode(secondNonces[0].Groups["nonce"].Value);
        Assert.All(firstNonces, match => Assert.Equal(firstNonce, WebUtility.HtmlDecode(match.Groups["nonce"].Value)));
        Assert.All(secondNonces, match => Assert.Equal(secondNonce, WebUtility.HtmlDecode(match.Groups["nonce"].Value)));
        Assert.Equal(32, Convert.FromBase64String(firstNonce).Length);
        Assert.Equal(32, Convert.FromBase64String(secondNonce).Length);
        Assert.NotEqual(firstNonce, secondNonce);
        Assert.Contains($"'nonce-{firstNonce}'", Assert.Single(firstResponse.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains($"'nonce-{secondNonce}'", Assert.Single(secondResponse.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("no-store", firstResponse.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", secondResponse.Headers.CacheControl?.ToString());
        Assert.Contains("\"withDefaultFonts\":false", firstBody);
        Assert.Contains("\"agent\":{\"disabled\":true}", firstBody);
        Assert.DoesNotContain("cdn.jsdelivr.net", firstBody);
    }
}
