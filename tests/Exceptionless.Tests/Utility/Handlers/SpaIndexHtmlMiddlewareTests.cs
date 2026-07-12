using System.Text;
using System.Text.Json;
using Exceptionless.Web;
using Exceptionless.Web.Security;
using Exceptionless.Web.Utility.Handlers;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Scalar.AspNetCore;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class SpaIndexHtmlMiddlewareTests
{
    [Theory]
    [InlineData("/index.html", "index.html")]
    [InlineData("/next/index.html", "next/index.html")]
    public async Task InvokeAsync_IndexRequest_AddsNonceAndDisablesCaching(string requestPath, string filePath)
    {
        const string html = "<html><body><script src=\"/app.js\"></script><SCRIPT nonce='old'>start();</SCRIPT></body></html>";
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            new Dictionary<string, string> { [filePath] = html },
            context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext(requestPath);
        context.Response.Headers.ETag = "\"cached\"";
        context.Response.Headers.LastModified = DateTimeOffset.UtcNow.ToString("R");

        await middleware.InvokeAsync(context, new TestNonceService("fresh-nonce"));

        string responseBody = ReadResponseBody(context);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.ETag));
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.LastModified));
        Assert.Equal(2, CountOccurrences(responseBody, "<script nonce=\"fresh-nonce\""));
        Assert.DoesNotContain("nonce='old'", responseBody, StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetByteCount(responseBody), context.Response.ContentLength);
    }

    [Theory]
    [InlineData("<script nonce=\"old\" src=\"/app.js\"></script>")]
    [InlineData("<script src=\"/app.js\" nonce='old'></script>")]
    [InlineData("<script nonce=old></script>")]
    [InlineData("<script nonce async></script>")]
    public void AddScriptNonce_ScriptWithExistingNonce_ReplacesNonce(string html)
    {
        string result = SpaIndexHtmlMiddleware.AddScriptNonce(html, "new");

        Assert.Equal(1, CountOccurrences(result, "nonce=\"new\""));
        Assert.DoesNotContain("old", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AddScriptNonce_QuotedGreaterThanInAttribute_PreservesOpeningTag()
    {
        const string html = "<script data-state=\"ready > pending\" nonce></script>";

        string result = SpaIndexHtmlMiddleware.AddScriptNonce(html, "new");

        Assert.Equal("<script nonce=\"new\" data-state=\"ready > pending\"></script>", result);
    }

    [Fact]
    public void AddScriptNonce_InlineScriptContainsScriptLikeText_PreservesContent()
    {
        const string html = "<script>const marker = \"<script>\";</script>";

        string result = SpaIndexHtmlMiddleware.AddScriptNonce(html, "new");

        Assert.Equal("<script nonce=\"new\">const marker = \"<script>\";</script>", result);
    }

    [Fact]
    public void AddScriptNonce_SimilarlyNamedAttributes_PreservesAttributes()
    {
        const string html = "<script data-nonce=\"keep\" noncevalue=\"keep\" nonce-value=\"keep\"></script>";

        string result = SpaIndexHtmlMiddleware.AddScriptNonce(html, "new");

        Assert.Contains("data-nonce=\"keep\"", result, StringComparison.Ordinal);
        Assert.Contains("noncevalue=\"keep\"", result, StringComparison.Ordinal);
        Assert.Contains("nonce-value=\"keep\"", result, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result, "nonce=\"new\""));
    }

    [Fact]
    public async Task InvokeAsync_HeadIndexRequest_WritesHeadersWithoutBody()
    {
        const string html = "<html><script>start();</script></html>";
        var middleware = CreateMiddleware(new Dictionary<string, string> { ["index.html"] = html });
        var context = CreateContext("/index.html", HttpMethods.Head);

        await middleware.InvokeAsync(context, new TestNonceService("head-nonce"));

        string expectedResponse = "<html><script nonce=\"head-nonce\">start();</script></html>";
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(Encoding.UTF8.GetByteCount(expectedResponse), context.Response.ContentLength);
        Assert.Equal(String.Empty, ReadResponseBody(context));
    }

    [Fact]
    public async Task InvokeAsync_NonIndexRequest_CallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            new Dictionary<string, string> { ["app.js"] = "console.log('ok');" },
            context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext("/app.js");

        await middleware.InvokeAsync(context, new TestNonceService("unused"));

        Assert.True(nextCalled);
        Assert.Equal(String.Empty, ReadResponseBody(context));
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    public async Task InvokeAsync_MissingIndexFile_CallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            new Dictionary<string, string>(),
            context =>
            {
                nextCalled = true;
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        var context = CreateContext("/next/index.html");

        await middleware.InvokeAsync(context, new TestNonceService("unused"));

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    public async Task InvokeAsync_PostIndexRequest_CallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(
            new Dictionary<string, string> { ["index.html"] = "<script></script>" },
            context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var context = CreateContext("/index.html", HttpMethods.Post);

        await middleware.InvokeAsync(context, new TestNonceService("unused"));

        Assert.True(nextCalled);
        Assert.Equal(String.Empty, ReadResponseBody(context));
    }

    [Fact]
    public async Task Configure_IndexAndFallbackRoutes_ServeFreshNoncedHtml()
    {
        string webRoot = Path.Combine(Path.GetTempPath(), "Exceptionless-SpaIndexHtmlMiddlewareTests", Guid.NewGuid().ToString("N"));
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
                ("/legacy/deep-route", "root"),
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

            using HttpResponseMessage scalarResponse = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
            string scalarBody = await scalarResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            string scalarNonce = GetNonceAttribute(scalarBody);
            string scalarPolicy = scalarResponse.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Equal("no-store", scalarResponse.Headers.CacheControl?.ToString());
            Assert.Contains($"'nonce-{scalarNonce}'", scalarPolicy, StringComparison.Ordinal);
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
        string imageDirective = GetDirective(policy, "img-src");

        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("form-action 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("manifest-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("worker-src 'self' blob:", policy, StringComparison.Ordinal);
        Assert.Contains("frame-src 'self' https://*.js.stripe.com", policy, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("https://api.stripe.com", policy, StringComparison.Ordinal);
        Assert.Contains("img-src 'self' data: blob: https://*.stripe.com https://*.link.com", policy, StringComparison.Ordinal);
        Assert.Contains("https://uploads.intercomcdn.com", imageDirective, StringComparison.Ordinal);
        Assert.Contains("wss://*.intercom-messenger.com", policy, StringComparison.Ordinal);
        Assert.Contains("'nonce-policy-nonce'", scriptDirective, StringComparison.Ordinal);
        Assert.Contains("'strict-dynamic'", scriptDirective, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'", scriptDirective, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-eval'", scriptDirective, StringComparison.Ordinal);
        Assert.DoesNotContain("https://cdn.jsdelivr.net", scriptDirective, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("intercomcdn.eu", policy, StringComparison.Ordinal);
        Assert.DoesNotContain(".eu.intercom.io", policy, StringComparison.Ordinal);
        Assert.DoesNotContain(".au.intercom.io", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("au.intercomcdn.com", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("static.au.intercomassets.com", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("intercom-attachments.eu", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("au.intercom-attachments.com", policy, StringComparison.Ordinal);

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

    private static SpaIndexHtmlMiddleware CreateMiddleware(IReadOnlyDictionary<string, string> files, RequestDelegate? next = null)
    {
        return new SpaIndexHtmlMiddleware(
            next ?? (_ => Task.CompletedTask),
            new InMemoryFileProvider(files));
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
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseCsp(FrontendContentSecurityPolicy.Configure);
                    app.UseDefaultFiles();
                    app.UseMiddleware<SpaIndexHtmlMiddleware>();
                    app.UseStaticFiles();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapScalarApiReference("/docs", (options, context) =>
                            options.WithNonce(context.RequestServices.GetRequiredService<ICspNonceService>().GetNonce()));
                        endpoints.MapFallback("{**slug:nonfile}", Startup.CreateSpaFallbackRequestDelegate(endpoints));
                    });
                }))
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static DefaultHttpContext CreateContext(string path, string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadResponseBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
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

    private sealed class InMemoryFileProvider(IReadOnlyDictionary<string, string> files) : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath)
        {
            return files.TryGetValue(subpath.TrimStart('/'), out string? content)
                ? new InMemoryFileInfo(subpath, content)
                : new NotFoundFileInfo(subpath);
        }

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class InMemoryFileInfo(string name, string content) : IFileInfo
    {
        public bool Exists => true;
        public long Length => Encoding.UTF8.GetByteCount(content);
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false);
    }
}
