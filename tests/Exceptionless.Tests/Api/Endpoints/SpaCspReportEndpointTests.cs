using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Exceptionless.AspNetCore;
using Exceptionless.Core;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Exceptionless.Web.Api.Endpoints;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Xunit;
using Joonasw.AspNetCore.SecurityHeaders;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class SpaCspReportEndpointTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public async Task PostAsync_BrowserReport_SubmitsOneSanitizedError()
    {
        // Arrange
        const string report = """{"csp-report":{"document-uri":"https://localhost/reset-password?token=private-token","blocked-uri":"https://localhost/_app/env.js?v=private-version","effective-directive":"script-src-elem","disposition":"enforce"}}""";
        string[] reports =
        [
            report.Replace("enforce", "report", StringComparison.Ordinal),
            report.Replace("script-src-elem", "style-src-elem", StringComparison.Ordinal),
            report.Replace("https://localhost/_app/", "https://other.localhost/_app/", StringComparison.Ordinal),
            report.Replace("https://localhost", "https://other.localhost", StringComparison.Ordinal),
            report.Replace("https://localhost/_app/env.js?v=private-version", "inline", StringComparison.Ordinal),
            report.Replace("script-src-elem", "\\uDEAD", StringComparison.Ordinal),
            "not JSON",
            report + new string(' ', 16 * 1024),
            report,
            report
        ];
        var submitted = new ConcurrentQueue<Event>();
        using var telemetry = new ExceptionlessClient(configuration =>
        {
            configuration.ApiKey = "00000000000000000000000000000000";
            configuration.UseInMemoryStorage();
        });
        telemetry.SubmittingEvent += (_, args) =>
        {
            submitted.Enqueue(args.Event);
            args.Cancel = true;
        };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ICacheClient, InMemoryCacheClient>();
        builder.Services.AddSingleton(provider =>
        {
            telemetry.Configuration.AddPlugin(new ExceptionlessAspNetCorePlugin(provider.GetRequiredService<IHttpContextAccessor>()));
            return telemetry;
        });
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddCsp(nonceByteAmount: 32);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseCors();
        app.UseCsp(csp => FrontendContentSecurityPolicy.Configure(csp, new NullFileProvider(), new AppOptions()));
        app.MapGet("/login", () => TypedResults.Ok());
        app.MapSpaCspReportEndpoint();
        await app.StartAsync(TestCancellationToken);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Referrer = new Uri("https://localhost/reset-password?token=private-referrer");
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v2/csp-reports");
        preflight.Headers.Add("Origin", "https://other.localhost");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        using var loginResponse = await client.GetAsync("/login", TestCancellationToken);
        using var preflightResponse = await client.SendAsync(preflight, TestCancellationToken);
        var statuses = new List<HttpStatusCode>();
        foreach (string body in reports)
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/csp-report");
            using var response = await client.PostAsync("/api/v2/csp-reports", content, TestCancellationToken);
            statuses.Add(response.StatusCode);
        }

        // Assert
        string policyHeader = Assert.Single(loginResponse.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("report-uri /api/v2/csp-reports", policyHeader.Split(';', StringSplitOptions.TrimEntries));
        Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.NoContent, status));
        var submittedEvent = Assert.Single(submitted);
        Assert.Equal(Event.KnownTypes.Error, submittedEvent.Type);
        Assert.Equal("Content Security Policy blocked the SPA runtime configuration (env.js)", submittedEvent.Message);
        Assert.Contains("csp", submittedEvent.Tags!);
        using var error = JsonDocument.Parse(Assert.IsType<string>(submittedEvent.Data![Event.KnownDataKeys.SimpleError]));
        Assert.Equal("ContentSecurityPolicyViolation", error.RootElement.GetProperty("type").GetString());
        string serialized = new DefaultJsonSerializer().Serialize(submittedEvent);
        Assert.DoesNotContain("private-token", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-version", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-referrer", serialized, StringComparison.Ordinal);
    }
}
