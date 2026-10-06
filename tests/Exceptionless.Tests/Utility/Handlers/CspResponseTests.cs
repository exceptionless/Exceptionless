using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Exceptionless.Web.Security;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class CspResponseTests
{
    [Fact]
    public async Task Configure_StaticSpa_TrustsPublishedHashWithoutRewritingResponses()
    {
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        string hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("start()")));
        string html = $"""<meta http-equiv="Content-Security-Policy" content="script-src 'strict-dynamic' 'sha256-{hash}'"><script>start()</script>""";
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), html, TestContext.Current.CancellationToken);

        try
        {
            using IHost host = await CreatePipelineHostAsync(webRoot);
            using HttpClient client = host.GetTestClient();
            foreach (string path in new[] { "/", "/index.html", "/deep-route" })
            {
                using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                string policy = response.Headers.GetValues("Content-Security-Policy").Single();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(html, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Contains($"'sha256-{hash}'", policy);
                Assert.Contains("'strict-dynamic'", policy);
                Assert.Contains("connect-src * ws:", policy);
                Assert.Contains("upgrade-insecure-requests", policy);
                Assert.Contains("object-src 'none'", policy);
                Assert.Contains("base-uri 'none'", policy);
                Assert.Contains("frame-ancestors 'none'", policy);
                Assert.DoesNotContain("'unsafe-eval'", policy);
                Assert.DoesNotContain("'unsafe-inline'", policy.Split(';').Single(d => d.TrimStart().StartsWith("script-src ", StringComparison.Ordinal)));
            }

            // A later injected script is not authorized by hashing the response body.
            const string injectedScript = "injected()";
            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), html + $"<script>{injectedScript}</script>", TestContext.Current.CancellationToken);
            using HttpResponseMessage injectedResponse = await client.GetAsync("/index.html", TestContext.Current.CancellationToken);
            Assert.DoesNotContain(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(injectedScript))), injectedResponse.Headers.GetValues("Content-Security-Policy").Single());
            Assert.DoesNotContain("nonce=", await injectedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Configure_Scalar_UsesFreshNoncesOnlyForItsOwnScripts()
    {
        string webRoot = Path.Combine(Path.GetTempPath(), $"exceptionless-csp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        try
        {
            using IHost host = await CreatePipelineHostAsync(webRoot);
            using HttpClient client = host.GetTestClient();
            string? previousNonce = null;
            for (int index = 0; index < 2; index++)
            {
                using HttpResponseMessage response = await client.GetAsync("/docs/", TestContext.Current.CancellationToken);
                string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                string policy = response.Headers.GetValues("Content-Security-Policy").Single();
                MatchCollection nonces = Regex.Matches(body, "<script\\b[^>]*\\bnonce=\"(?<nonce>[^\"]+)\"");
                Assert.Equal(3, nonces.Count);
                string nonce = WebUtility.HtmlDecode(nonces[0].Groups["nonce"].Value);
                Assert.All(nonces, match => Assert.Equal(nonce, WebUtility.HtmlDecode(match.Groups["nonce"].Value)));
                Assert.Equal(32, Convert.FromBase64String(nonce).Length);
                Assert.NotEqual(previousNonce, nonce);
                Assert.Contains($"'nonce-{nonce}'", policy);
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                Assert.Contains("<script>injected()</script>", body);
                Assert.Contains("\"withDefaultFonts\":false", body);
                Assert.DoesNotContain("cdn.jsdelivr.net", body);
                previousNonce = nonce;
            }
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private static async Task<IHost> CreatePipelineHostAsync(string webRoot)
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureWebHost(builder => builder.UseContentRoot(webRoot).UseWebRoot(webRoot).UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddCsp(nonceByteAmount: 32);
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    using var files = new PhysicalFileProvider(webRoot);
                    app.UseCsp(csp => FrontendContentSecurityPolicy.Configure(csp, files, upgradeInsecureRequests: true));
                    app.UseDefaultFiles();
                    app.UseStaticFiles();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapScalarApiReference("/docs", (options, context) => options
                            .WithNonce(context.RequestServices.GetRequiredService<ICspNonceService>().GetNonce())
                            .DisableDefaultFonts()
                            .AddHeaderContent("<script>injected()</script>"));
                        endpoints.MapFallback("{**slug:nonfile}", Exceptionless.Web.Program.CreateRequestDelegate(endpoints, "/index.html"));
                    });
                })).Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }
}
