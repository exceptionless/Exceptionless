using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Exceptionless.Web.Security;
using Foundatio.Xunit;
using Joonasw.AspNetCore.SecurityHeaders;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class CspResponseTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public async Task Configure_PublishedSpa_TrustsStartupHashWithoutRewritingResponses()
    {
        // Arrange
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        string hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("start()")));
        string html = $"""<meta http-equiv="Content-Security-Policy" content="script-src 'strict-dynamic' 'sha256-{hash}'"><script>start()</script>""";
        string injectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("injected()")));
        string changedHtml = html + "<script>injected()</script>";

        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), html, TestContext.Current.CancellationToken);
            using IHost host = await CreateMiddlewareHostAsync(webRoot);
            using HttpClient client = host.GetTestClient();
            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), changedHtml, TestContext.Current.CancellationToken);

            // Act
            using HttpResponseMessage response = await client.GetAsync("/index.html", TestContext.Current.CancellationToken);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            string policy = response.Headers.GetValues("Content-Security-Policy").Single();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(changedHtml, body);
            Assert.Contains("default-src 'none'", policy);
            Assert.Contains($"'sha256-{hash}'", policy);
            Assert.DoesNotContain(injectedHash, policy);
            Assert.DoesNotContain("nonce=", body);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigureScalar_TwoRequests_UsesFreshNoncesOnlyForScalarScripts()
    {
        // Arrange
        using IHost host = await CreateMiddlewareHostAsync();
        using HttpClient client = host.GetTestClient();

        // Act
        using HttpResponseMessage firstResponse = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
        string firstBody = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string secondBody = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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
        Assert.Contains("<script>injected()</script>", firstBody);
        Assert.Contains("<script>injected()</script>", secondBody);
        Assert.Contains("\"withDefaultFonts\":false", firstBody);
        Assert.Contains("\"agent\":{\"disabled\":true}", firstBody);
        Assert.DoesNotContain("cdn.jsdelivr.net", firstBody);
    }

    [Fact]
    public async Task Configure_ConfiguredOrigins_RestrictsConnectionsWithoutTrustingRequestHeaders()
    {
        // Arrange
        using IHost host = await CreateMiddlewareHostAsync(siteBaseUrl: "https://site.localhost:8111", apiUrl: "https://api.localhost:9443/backend?ignored=true");
        using HttpClient client = host.GetTestClient();
        client.DefaultRequestHeaders.Host = "poisoned.localhost:4444";
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "forwarded.localhost:5555");
        client.DefaultRequestHeaders.Add("Forwarded", "host=forwarded.localhost:5555;proto=http");

        // Act
        using HttpResponseMessage response = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
        string policy = response.Headers.GetValues("Content-Security-Policy").Single();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string connections = Assert.Single(policy.Split(';').Select(directive => directive.Trim()), directive => directive.StartsWith("connect-src ", StringComparison.Ordinal));
        Assert.Equal([
            "connect-src", "'self'", "https://*.exceptionless.io", "https://api.stripe.com",
            "https://link.com", "https://*.link.com", "https://*.intercom.io", "wss://*.intercom.io",
            "https://*.intercom-messenger.com", "wss://*.intercom-messenger.com",
            "https://site.localhost:8111", "wss://site.localhost:8111", "https://api.localhost:9443", "wss://api.localhost:9443"
        ], connections.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Exercise the middleware boundary without starting unrelated databases or copying app routing.
    private async Task<IHost> CreateMiddlewareHostAsync(string? webRoot = null, string? siteBaseUrl = null, string? apiUrl = null)
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSingleton<ILoggerFactory>(Log))
            .ConfigureWebHost(builder =>
            {
                if (webRoot is not null)
                    builder.UseContentRoot(webRoot).UseWebRoot(webRoot);
                builder.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddCsp(nonceByteAmount: 32);
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                        app.UseCsp(csp => FrontendContentSecurityPolicy.Configure(csp, environment.WebRootFileProvider,
                            upgradeInsecureRequests: true, siteBaseUrl, apiUrl));
                        app.UseStaticFiles();
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapScalarApiReference("/docs", (options, context) =>
                        {
                            Exceptionless.Web.Program.ConfigureScalar(options, context);
                            options.AddHeaderContent("<script>injected()</script>");
                        }));
                    });
            }).Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }
}
