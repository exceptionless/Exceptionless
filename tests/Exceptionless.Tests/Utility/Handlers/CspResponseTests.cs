using System.Text.Json;
using System.Text.RegularExpressions;
using Exceptionless.Web.Security;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class CspResponseTests
{
    [Theory]
    [InlineData("GET", "If-Modified-Since", "Mon, 01 Jan 2024 00:00:00 GMT")]
    [InlineData("GET", "If-None-Match", "*")]
    [InlineData("GET", "Range", "bytes=0-15")]
    [InlineData("GET", "Accept", "application/json")]
    [InlineData("HEAD", "Accept", "text/html")]
    public async Task InjectCspNonceAsync_HtmlRequest_PreservesFreshResponse(string method, string header, string value)
    {
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<script src=\"/app.js\"></script>", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Path.Combine(webRoot, "index.html"), new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        try
        {
            using IHost host = await CreatePipelineHostAsync(webRoot);
            using HttpClient client = host.GetTestClient();
            using var request = new HttpRequestMessage(new HttpMethod(method), "/index.html");
            request.Headers.TryAddWithoutValidation(header, value);
            using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Null(response.Headers.ETag);
            Assert.Null(response.Content.Headers.LastModified);
            Assert.Empty(response.Headers.AcceptRanges);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (method == "HEAD")
                Assert.Empty(body);
            else
                Assert.Contains($"'nonce-{GetScriptNonce(body)}'", response.Headers.GetValues("Content-Security-Policy").Single());
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void AddScriptNonce_NonceTextInsideAttribute_PreservesAttribute()
    {
        const string html = "<script data-note=\"a nonce='keep'\" nonce='old'></script>";

        Assert.Equal("<script nonce=\"new\" data-note=\"a nonce='keep'\"></script>", AddNonceToTrustedHtml(html, "new"));
    }

    [Theory]
    [InlineData("<script nonce=\"old\" src=\"/app.js\"></script>")]
    [InlineData("<script src=\"/app.js\" nonce='old'></script>")]
    [InlineData("<script nonce=old></script>")]
    [InlineData("<script nonce async></script>")]
    public void AddScriptNonce_ScriptWithExistingNonce_ReplacesNonce(string html)
    {
        string result = AddNonceToTrustedHtml(html, "new");

        Assert.Equal(1, CountOccurrences(result, "nonce=\"new\""));
        Assert.DoesNotContain("old", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AddScriptNonce_QuotedGreaterThanInAttribute_PreservesOpeningTag()
    {
        const string html = "<script data-state=\"ready > pending\" nonce></script>";

        string result = AddNonceToTrustedHtml(html, "new");

        Assert.Equal("<script nonce=\"new\" data-state=\"ready > pending\"></script>", result);
    }

    [Fact]
    public void AddScriptNonce_InlineScriptContainsScriptLikeText_PreservesContent()
    {
        const string html = "<script>const marker = \"<script>\";</script>";

        string result = AddNonceToTrustedHtml(html, "new");

        Assert.Equal("<script nonce=\"new\">const marker = \"<script>\";</script>", result);
    }

    [Fact]
    public void AddScriptNonce_SimilarlyNamedAttributes_PreservesAttributes()
    {
        const string html = "<script data-nonce=\"keep\" noncevalue=\"keep\" nonce-value=\"keep\"></script>";

        string result = AddNonceToTrustedHtml(html, "new");

        Assert.Contains("data-nonce=\"keep\"", result, StringComparison.Ordinal);
        Assert.Contains("noncevalue=\"keep\"", result, StringComparison.Ordinal);
        Assert.Contains("nonce-value=\"keep\"", result, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result, "nonce=\"new\""));
    }

    [Fact]
    public void AddNonce_UntrustedOrModifiedScripts_DoesNotAuthorizeThem()
    {
        const string trustedHtml = "<script src=\"/app.js\"></script><script>start()</script>";
        const string injectedHtml = trustedHtml
            + "<script>injected()</script><script src=\"/payload.js\"></script>"
            + "<script src=\"https://untrusted.example/payload.js\"></script>"
            + "<script nonce=\"attacker\">start();injected()</script>"
            + "<script src=\"/app.js\" onload=\"injected()\"></script>";

        string result = AddNonceToTrustedHtml(injectedHtml, "fresh", trustedHtml);

        Assert.StartsWith("<script nonce=\"fresh\" src=\"/app.js\"></script><script nonce=\"fresh\">start()</script>", result);
        Assert.Equal(2, CountOccurrences(result, "nonce=\"fresh\""));
        Assert.EndsWith(injectedHtml[trustedHtml.Length..], result);
    }

    [Fact]
    public async Task Configure_IndexAndFallbackRoutes_ServeFreshNoncedHtml()
    {
        string webRoot = Path.Combine(Path.GetTempPath(), "Exceptionless-CspResponseTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(webRoot, "next"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<html><body>root<script src=\"/root.js\"></script></body></html>", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "next", "index.html"), "<html><body>next<script>start();</script></body></html>", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "app.js"), "console.log('static');", TestContext.Current.CancellationToken);

        try
        {
            using IHost host = await CreatePipelineHostAsync(webRoot);
            using HttpClient client = host.GetTestClient();
            string? previousNonce = null;
            (string Path, string Marker)[] routes =
            [
                ("/", "root"),
                ("/index.html", "root"),
                ("/next/", "next"),
                ("/next/index.html", "next"),
                ("/next/deep-route", "next")
            ];

            foreach ((string path, string marker) in routes)
            {
                using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                string policy = response.Headers.GetValues("Content-Security-Policy").Single();
                string nonce = GetScriptNonce(body);

                Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
                Assert.Contains(marker, body, StringComparison.Ordinal);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                Assert.Contains($"'nonce-{nonce}'", policy, StringComparison.Ordinal);
                Assert.Contains("'strict-dynamic'", policy, StringComparison.Ordinal);
                Assert.NotEqual(previousNonce, nonce);
                previousNonce = nonce;
            }

            using HttpResponseMessage staticResponse = await client.GetAsync("/app.js", TestContext.Current.CancellationToken);
            Assert.Equal("console.log('static');", await staticResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.NotEqual("no-store", staticResponse.Headers.CacheControl?.ToString());

            using var poisonedRequest = new HttpRequestMessage(HttpMethod.Get, "/next/");
            poisonedRequest.Headers.Host = "untrusted.example";
            poisonedRequest.Headers.Add("X-Forwarded-Host", "forwarded.example");
            poisonedRequest.Headers.Add("X-Forwarded-Proto", "http");
            using HttpResponseMessage poisonedResponse = await client.SendAsync(poisonedRequest, TestContext.Current.CancellationToken);
            string poisonedPolicy = poisonedResponse.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Contains("wss://app.example.test", poisonedPolicy, StringComparison.Ordinal);
            Assert.DoesNotContain("untrusted.example", poisonedPolicy, StringComparison.Ordinal);
            Assert.DoesNotContain("forwarded.example", poisonedPolicy, StringComparison.Ordinal);

            using HttpResponseMessage scalarResponse = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
            string scalarBody = await scalarResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            string scalarNonce = GetNonceAttribute(scalarBody);
            string scalarPolicy = scalarResponse.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Equal("no-store", scalarResponse.Headers.CacheControl?.ToString());
            Assert.Contains($"'nonce-{scalarNonce}'", scalarPolicy, StringComparison.Ordinal);
            Assert.Contains("scalar.js", scalarBody, StringComparison.Ordinal);
            Assert.Contains("<script>untrustedHeader()</script>", scalarBody, StringComparison.Ordinal);
            MatchCollection scalarScriptNonces = Regex.Matches(scalarBody, "<script\\b[^>]*\\bnonce=\"(?<nonce>[^\"]+)\"");
            Assert.Equal(3, scalarScriptNonces.Count);
            Assert.All(scalarScriptNonces, script => Assert.Equal(scalarNonce, System.Net.WebUtility.HtmlDecode(script.Groups["nonce"].Value)));
            Assert.Contains("scalar.aspnetcore.js", scalarBody, StringComparison.Ordinal);
            Assert.Contains("\"withDefaultFonts\":false", scalarBody, StringComparison.Ordinal);
            Assert.DoesNotContain("cdn.jsdelivr.net", scalarBody, StringComparison.Ordinal);
            using HttpResponseMessage scalarScriptResponse = await client.GetAsync("/docs/scalar.js", TestContext.Current.CancellationToken);
            Assert.Equal(StatusCodes.Status200OK, (int)scalarScriptResponse.StatusCode);
            Assert.StartsWith("text/javascript", scalarScriptResponse.Content.Headers.ContentType?.ToString());

            using HttpResponseMessage nextScalarResponse = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
            string nextScalarBody = await nextScalarResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            string nextScalarNonce = GetNonceAttribute(nextScalarBody);
            Assert.NotEqual(scalarNonce, nextScalarNonce);
            Assert.Contains($"'nonce-{nextScalarNonce}'", nextScalarResponse.Headers.GetValues("Content-Security-Policy").Single());
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ConfigureContentSecurityPolicy_DefaultAndScriptDirectives_AreLockedDown()
    {
        var builder = new CspBuilder();
        FrontendContentSecurityPolicy.Configure(builder);
        var options = builder.BuildCspOptions();

        (_, string policy) = options.ToString(new TestNonceService("policy-nonce"));
        string scriptDirective = GetDirective(policy, "script-src");
        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("'nonce-policy-nonce'", scriptDirective, StringComparison.Ordinal);
        Assert.Contains("'strict-dynamic'", scriptDirective, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", scriptDirective, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-eval'", scriptDirective, StringComparison.Ordinal);

        var apiContext = new DefaultHttpContext();
        apiContext.Request.Path = "/api/v2/about";
        var sendingHeaderContext = new CspSendingHeaderContext(apiContext);
        await options.OnSendingHeader(sendingHeaderContext);
        Assert.True(sendingHeaderContext.ShouldNotSend);
    }

    [Fact]
    public void ConfigureContentSecurityPolicy_DefaultPolicy_MatchesCanonicalCrossRuntimeContract()
    {
        var builder = new CspBuilder();
        FrontendContentSecurityPolicy.Configure(builder);
        (_, string policy) = builder.BuildCspOptions().ToString(new TestNonceService("contract-nonce"));

        IReadOnlyDictionary<string, string[]> expected = ReadPolicyContract();
        IReadOnlyDictionary<string, string[]> actual = NormalizePolicy(policy);

        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach ((string directive, string[] expectedSources) in expected)
            Assert.Equal(expectedSources, actual[directive]);
    }

    [Fact]
    public void ConfigureContentSecurityPolicy_IntercomMessenger_AllowsHttpsAndWebSocketConnections()
    {
        var builder = new CspBuilder();
        FrontendContentSecurityPolicy.Configure(builder);
        (_, string policy) = builder.BuildCspOptions().ToString(new TestNonceService("intercom-nonce"));

        IReadOnlyDictionary<string, string[]> directives = NormalizePolicy(policy);
        string[] messengerSources = directives["connect-src"]
            .Where(source => source.Contains("intercom-messenger.com", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["https://*.intercom-messenger.com", "wss://*.intercom-messenger.com"], messengerSources);
        Assert.Contains("'strict-dynamic'", directives["script-src"]);
        Assert.DoesNotContain("'unsafe-inline'", directives["script-src"]);
        Assert.DoesNotContain("'unsafe-eval'", directives["script-src"]);
    }

    [Fact]
    public void ConfigureContentSecurityPolicy_ModernSite_ExcludesUnusedVendorSources()
    {
        var builder = new CspBuilder();
        FrontendContentSecurityPolicy.Configure(builder);
        (_, string policy) = builder.BuildCspOptions().ToString(new TestNonceService("modern-nonce"));

        Assert.DoesNotContain("fonts.googleapis.com", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts.gstatic.com", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("user-images.githubusercontent.com", policy, StringComparison.Ordinal);
        IReadOnlyDictionary<string, string[]> directives = NormalizePolicy(policy);
        Assert.DoesNotContain("https:", directives["connect-src"]);
        Assert.DoesNotContain("ws:", directives["connect-src"]);
        Assert.DoesNotContain("wss:", directives["connect-src"]);
        foreach (string unusedSource in new[]
        {
            "config.exceptionless.io", "heartbeat.exceptionless.io", "maps.googleapis.com", "cdn.jsdelivr.net", "intercom-sheets.com", "intercom-reporting.com",
            "youtube.com", "vimeo.com", "wistia.net", "intercom-attachments-", "uploads.intercom",
            "downloads.intercom", "gifs.intercom", "video-messages.intercom", "messenger-apps.intercom", "intercom.help"
        })
            Assert.DoesNotContain(unusedSource, policy, StringComparison.Ordinal);
        Assert.DoesNotContain("https://*.stripe.com", directives["img-src"]);
        Assert.Contains("https://api.stripe.com", directives["connect-src"]);
        Assert.Equal(["'self'"], directives["form-action"]);
        Assert.Equal(["'self'"], directives["worker-src"]);
    }

    [Theory]
    [InlineData("https://app.example.test/next/?query=value#fragment", "wss://app.example.test")]
    [InlineData("https://app.example.test:8443/next", "wss://app.example.test:8443")]
    [InlineData("http://localhost:7110", "ws://localhost:7110")]
    [InlineData("http://localhost:80", "ws://localhost")]
    [InlineData("https://[::1]:8443/next", "wss://[::1]:8443")]
    public void ConfigureContentSecurityPolicy_ConfiguredSite_AddsOnlyItsWebSocketOrigin(string siteBaseUrl, string expectedOrigin)
    {
        var builder = new CspBuilder();
        FrontendContentSecurityPolicy.Configure(builder, siteBaseUrl);
        (_, string policy) = builder.BuildCspOptions().ToString(new TestNonceService("origin-nonce"));

        string[] connections = NormalizePolicy(policy)["connect-src"];
        Assert.Contains(expectedOrigin, connections);
        Assert.DoesNotContain("ws:", connections);
        Assert.DoesNotContain("wss:", connections);
        Assert.DoesNotContain("query=value", policy, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/next")]
    [InlineData("ftp://app.example.test")]
    [InlineData("https://user:password@app.example.test")]
    [InlineData("https://*.example.test")]
    public void ConfigureContentSecurityPolicy_InvalidSite_RejectsConfiguration(string siteBaseUrl)
    {
        Assert.Throws<ArgumentException>(() => FrontendContentSecurityPolicy.Configure(new CspBuilder(), siteBaseUrl));
    }

    [Fact]
    public void AddCsp_SeparateScopes_ProvidesDistinct32ByteNonces()
    {
        var services = new ServiceCollection();
        services.AddCsp(nonceByteAmount: 32);
        using ServiceProvider provider = services.BuildServiceProvider();
        string firstNonce;

        using (IServiceScope firstScope = provider.CreateScope())
        {
            var nonceService = firstScope.ServiceProvider.GetRequiredService<ICspNonceService>();
            firstNonce = nonceService.GetNonce();
            Assert.Equal(firstNonce, nonceService.GetNonce());
            Assert.Equal(32, Convert.FromBase64String(firstNonce).Length);
        }

        using IServiceScope secondScope = provider.CreateScope();
        string secondNonce = secondScope.ServiceProvider.GetRequiredService<ICspNonceService>().GetNonce();
        Assert.Equal(32, Convert.FromBase64String(secondNonce).Length);
        Assert.NotEqual(firstNonce, secondNonce);
    }

    private static async Task<IHost> CreatePipelineHostAsync(string webRoot)
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseContentRoot(webRoot)
                .UseWebRoot(webRoot)
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddCsp(nonceByteAmount: 32);
                    services.AddSingleton<FrontendScriptNonces>();
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseCsp(csp => FrontendContentSecurityPolicy.Configure(csp, "https://app.example.test/next"));
                    app.UseDefaultFiles();
                    app.Use(Exceptionless.Web.Program.InjectCspNonceAsync);
                    app.UseStaticFiles();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapScalarApiReference("/docs", (options, context) => options
                            .WithNonce(context.RequestServices.GetRequiredService<ICspNonceService>().GetNonce())
                            .DisableDefaultFonts()
                            .AddHeaderContent("<script>untrustedHeader()</script>"));
                        endpoints.MapFallback("{**slug:nonfile}", Exceptionless.Web.Program.CreateRequestDelegate(endpoints, "/index.html"));
                    });
                }))
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static string AddNonceToTrustedHtml(string html, string nonce, string? trustedHtml = null)
    {
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        try
        {
            File.WriteAllText(Path.Combine(webRoot, "index.html"), trustedHtml ?? html);
            using var files = new PhysicalFileProvider(webRoot);
            return new FrontendScriptNonces(files).AddNonce(html, nonce);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private static int CountOccurrences(string value, string search)
    {
        return value.Split(search, StringSplitOptions.None).Length - 1;
    }

    private static string GetScriptNonce(string html)
    {
        Assert.Contains("<script", html, StringComparison.OrdinalIgnoreCase);
        return GetNonceAttribute(html);
    }

    private static string GetNonceAttribute(string html)
    {
        const string noncePrefix = "nonce=\"";
        int nonceStart = html.IndexOf(noncePrefix, StringComparison.Ordinal);
        Assert.True(nonceStart >= 0);
        nonceStart += noncePrefix.Length;
        int nonceEnd = html.IndexOf('"', nonceStart);
        Assert.True(nonceEnd > nonceStart);
        return System.Net.WebUtility.HtmlDecode(html[nonceStart..nonceEnd]);
    }

    private static string GetDirective(string policy, string directiveName)
    {
        return policy.Split(';').Single(directive => directive.StartsWith(directiveName + " ", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string[]> NormalizePolicy(string policy)
    {
        return policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(
                parts => parts[0],
                parts => parts.Skip(1)
                    .Where(source => !source.StartsWith("'nonce-", StringComparison.Ordinal))
                    .Order()
                    .ToArray());
    }

    private static IReadOnlyDictionary<string, string[]> ReadPolicyContract()
    {
        string contractPath = Path.Combine(AppContext.BaseDirectory, "Security", "frontend-content-security-policy.contract.json");
        var contract = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(contractPath));
        Assert.NotNull(contract);

        return contract.ToDictionary(entry => entry.Key, entry => entry.Value.Order().ToArray());
    }

    private sealed class TestNonceService(string nonce) : ICspNonceService
    {
        public string GetNonce() => nonce;
    }
}
