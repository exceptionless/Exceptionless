using System.IO.Compression;
using System.Net;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Web.Endpoints;
using Exceptionless.Web.Utility.Handlers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class EventIngestionV3RequestBodyMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_HighRatioV3Request_RaisesOnlyPerRequestDecompressedLimit()
    {
        var appOptions = new AppOptions
        {
            EventIngestionV3 = new EventIngestionV3Options
            {
                MaximumCompressedBodySize = 512,
                MaximumDecompressedBodySize = 4096
            }
        };
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, 0);
            kestrel.Limits.MaxRequestBodySize = 128;
        });
        builder.Services.AddSingleton(appOptions);
        builder.Services.AddRequestDecompression(options => options.DecompressionProviders.Remove("deflate"));

        await using WebApplication app = builder.Build();
        app.UseWhen(
            context => context.GetEndpoint()?.Metadata.GetMetadata<EventIngestionV3EndpointMetadata>() is not null,
            branch =>
            {
                branch.UseMiddleware<EventIngestionV3RequestBodyMiddleware>();
                branch.UseRequestDecompression();
            });
        app.MapPost("/api/v3/events", ReadRequestBodyAsync).WithMetadata(EventIngestionV3EndpointMetadata.Instance);
        app.MapPost("/api/v3/other", ReadRequestBodyAsync);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(addresses!.Addresses)) };
        byte[] decompressed = Encoding.UTF8.GetBytes(new string('x', 2048));
        byte[] compressed;
        await using (var output = new MemoryStream())
        {
            await using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                await gzip.WriteAsync(decompressed, TestContext.Current.CancellationToken);
            compressed = output.ToArray();
        }
        Assert.True(compressed.Length < 128);

        using var v3Content = new ByteArrayContent(compressed);
        v3Content.Headers.ContentEncoding.Add("gzip");
        using HttpResponseMessage v3Response = await client.PostAsync("/api/v3/events", v3Content, TestContext.Current.CancellationToken);
        using var otherContent = new ByteArrayContent(new byte[256]);
        using HttpResponseMessage otherResponse = await client.PostAsync("/api/v3/other", otherContent, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, v3Response.StatusCode);
        Assert.Equal("2048", await v3Response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, otherResponse.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_EarlyEndpointRejection_KeepsFiniteUnreadBodyDrainLimit()
    {
        var appOptions = new AppOptions
        {
            EventIngestionV3 = new EventIngestionV3Options
            {
                MaximumCompressedBodySize = 512,
                MaximumDecompressedBodySize = 4096
            }
        };
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(appOptions);

        await using WebApplication app = builder.Build();
        app.UseWhen(
            context => context.GetEndpoint()?.Metadata.GetMetadata<EventIngestionV3EndpointMetadata>() is not null,
            branch => branch.UseMiddleware<EventIngestionV3RequestBodyMiddleware>());
        app.MapPost("/api/v3/events", context =>
        {
            long? limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
            return context.Response.WriteAsync(limit?.ToString() ?? "unbounded");
        }).WithMetadata(EventIngestionV3EndpointMetadata.Instance);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(addresses!.Addresses)) };
        using HttpResponseMessage response = await client.PostAsync(
            "/api/v3/events",
            new ByteArrayContent([]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("4096", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task ReadRequestBodyAsync(HttpContext context)
    {
        await using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, context.RequestAborted);
        await context.Response.WriteAsync(body.Length.ToString(), context.RequestAborted);
    }
}
