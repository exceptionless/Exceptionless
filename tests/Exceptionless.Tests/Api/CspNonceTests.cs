using System.Text;
using System.Text.RegularExpressions;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class CspNonceTests
{
    [Fact]
    public async Task InjectCspNonceAsync_StaticHtmlResponses_UsesUniqueHeaderNonceForEveryScript()
    {
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(webRoot, "index.html"),
                """<script nonce="stale">const marker = "<script>";</script><script src="/app.js"></script>""",
                TestContext.Current.CancellationToken);

            using var fileProvider = new PhysicalFileProvider(webRoot);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCsp(nonceByteAmount: 32);
            services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(webRoot, fileProvider));
            await using var serviceProvider = services.BuildServiceProvider();

            var app = new ApplicationBuilder(serviceProvider);
            app.UseCsp(csp => csp.AllowScripts.FromSelf().AddNonce().WithStrictDynamic());
            app.Use(Exceptionless.Web.Program.InjectCspNonceAsync);
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
            RequestDelegate pipeline = app.Build();

            var responses = new List<(string Html, string Policy)>();
            for (int index = 0; index < 2; index++)
            {
                await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
                var context = new DefaultHttpContext
                {
                    RequestServices = scope.ServiceProvider
                };
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = "/index.html";
                context.Request.Headers.Accept = index == 0 ? "text/html" : "*/*";
                context.Response.Body = new MemoryStream();

                await pipeline(context);

                context.Response.Body.Position = 0;
                using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
                responses.Add((await reader.ReadToEndAsync(TestContext.Current.CancellationToken), context.Response.Headers.ContentSecurityPolicy.ToString()));
            }

            var nonces = responses
                .Select(response => Regex.Match(response.Policy, "'nonce-(?<nonce>[^']+)'").Groups["nonce"].Value)
                .ToArray();

            Assert.All(nonces, nonce => Assert.NotEmpty(nonce));
            Assert.NotEqual(nonces[0], nonces[1]);

            for (int index = 0; index < responses.Count; index++)
            {
                Assert.Equal(2, Regex.Matches(responses[index].Html, $"nonce=\"{Regex.Escape(nonces[index])}\"").Count);
                Assert.DoesNotContain("stale", responses[index].Html);
                Assert.Contains("""const marker = "<script>";""", responses[index].Html);
            }
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task InjectCspNonceAsync_NonHtmlResponse_PreservesResponse()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/status";
        context.Request.Headers.Accept = "text/html";
        context.Response.Body = new MemoryStream();

        await Exceptionless.Web.Program.InjectCspNonceAsync(context, async httpContext =>
        {
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.Headers.ETag = "\"current\"";
            await httpContext.Response.WriteAsync("""{"status":"ok"}""", httpContext.RequestAborted);
        });

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        Assert.Equal("""{"status":"ok"}""", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Equal("\"current\"", context.Response.Headers.ETag);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task InjectCspNonceAsync_RequestDoesNotAcceptHtml_DoesNotResolveNonceService()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = new MemoryStream();

        await Exceptionless.Web.Program.InjectCspNonceAsync(context, httpContext =>
            httpContext.Response.WriteAsync("content", httpContext.RequestAborted));

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        Assert.Equal("content", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    private sealed class TestWebHostEnvironment(string webRootPath, IFileProvider webRootFileProvider) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(CspNonceTests);
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = webRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = webRootFileProvider;
        public string WebRootPath { get; set; } = webRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = webRootFileProvider;
    }
}
