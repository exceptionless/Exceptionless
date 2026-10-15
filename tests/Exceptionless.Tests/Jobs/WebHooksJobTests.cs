using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public sealed class WebHooksJobTests(ITestOutputHelper output) : TestWithServices(output)
{
    private const string DestinationUrl = "https://example.com/webhook";

    [Theory]
    [InlineData(HttpStatusCode.OK, false, 0, true)]
    [InlineData(HttpStatusCode.OK, false, 0, false)]
    [InlineData(HttpStatusCode.Unauthorized, true, 0, false)]
    [InlineData(HttpStatusCode.Forbidden, true, 0, false)]
    [InlineData(HttpStatusCode.Gone, true, 0, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false, 1, false)]
    [InlineData(HttpStatusCode.InternalServerError, false, 1, false)]
    public async Task ProcessQueueEntryAsync_HttpResponse_PreservesDeliveryAndFailureBookkeeping(HttpStatusCode status, bool disabled, long errors, bool legacyPayload)
    {
        // Arrange
        var payloads = new ConcurrentQueue<(string? ContentType, string Body)>();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        server.MapPost("/webhook", async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            payloads.Enqueue((context.Request.ContentType, await reader.ReadToEndAsync(context.RequestAborted)));
            context.Response.StatusCode = (int)status;
        });
        await server.StartAsync(TestCancellationToken);
        string address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var hook = CreateWebHook(address + "/webhook");
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        var job = CreateJob(queue, hook, cache, GetService<IHttpClientFactory>());
        var notification = CreateNotification(hook);
        notification.Data = legacyPayload
            ? new VersionOnePlugin.VersionOneWebHookEvent(address)
            {
                Id = TestConstants.EventId, Message = "<event>", ProjectId = hook.ProjectId, ProjectName = "Test project",
                OrganizationId = hook.OrganizationId, OrganizationName = "Test organization",
                ErrorStackId = TestConstants.StackId, ErrorStackTitle = "Test stack", ErrorStackTags = []
            }
            : new WebHookEvent(address)
            {
                Id = TestConstants.EventId, Message = "<event>", ProjectId = hook.ProjectId, ProjectName = "Test project",
                OrganizationId = hook.OrganizationId, OrganizationName = "Test organization",
                StackId = TestConstants.StackId, StackTitle = "Test stack", StackTags = []
            };
        await queue.EnqueueAsync(notification);

        // Act
        var result = await job.RunAsync(TestCancellationToken);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.ToString());
        var payload = Assert.Single(payloads);
        Assert.Equal("application/json; charset=utf-8", payload.ContentType);
        using var json = JsonDocument.Parse(payload.Body);
        Assert.Equal(TestConstants.EventId, json.RootElement.GetProperty(legacyPayload ? "Id" : "id").GetString());
        Assert.Equal("<event>", json.RootElement.GetProperty(legacyPayload ? "Message" : "message").GetString());
        Assert.DoesNotContain("<event>", payload.Body);
        Assert.Equal(!disabled, hook.IsEnabled);
        Assert.Equal(errors, await cache.GetAsync<long>($"Project:{hook.ProjectId}:webhook:{hook.Id}:errors", 0));
        Assert.Equal(1, (await queue.GetQueueStatsAsync()).Completed);
        await server.StopAsync(TestCancellationToken);
    }

    [Fact]
    public async Task ProcessQueueEntryAsync_MissingWebHookId_LogsSafeDestinationWithoutSending()
    {
        // Arrange
        using var handler = new DelegateHandler((_, _) => throw new InvalidOperationException("Disabled notifications must not be sent"));
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        var hook = CreateWebHook("https://user:password@example.com/private-capability?secret-query=value");
        var notification = CreateNotification(hook);
        notification.WebHookId = null;
        var job = CreateJob(queue, hook, cache, CreateClientFactory(handler));
        await queue.EnqueueAsync(notification);

        // Act
        var result = await job.RunAsync(TestCancellationToken);

        // Assert
        Assert.True(result.IsCancelled);
        Assert.Equal(0, handler.RequestCount);
        var entry = Assert.Single(Log.LogEntries, entry => entry.LogLevel is LogLevel.Warning && entry.Message.StartsWith("WebHook notification is missing", StringComparison.Ordinal));
        Assert.Contains("https://example.com", entry.Message);
        Assert.DoesNotContain("password", entry.Message);
        Assert.DoesNotContain("private-capability", entry.Message);
        Assert.DoesNotContain("secret-query", entry.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessQueueEntryAsync_Response_DisposesWithoutBufferingEvenWhenBookkeepingFails(bool failBookkeeping)
    {
        // Arrange
        var content = new TrackingContent();
        var failure = new IOException("Failure bookkeeping failed");
        using var handler = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(failBookkeeping ? HttpStatusCode.BadGateway : HttpStatusCode.OK) { Content = content }));
        using var cache = new InMemoryCacheClient();
        var failingCache = new Mock<ICacheClient>(MockBehavior.Strict);
        failingCache.Setup(client => client.GetAsync<long>(It.IsAny<string>())).Returns((string key) => cache.GetAsync<long>(key));
        failingCache.Setup(client => client.SetAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan?>())).ThrowsAsync(failure);
        using var queue = new InMemoryQueue<WebHookNotification>();
        var hook = CreateWebHook(DestinationUrl);
        var job = CreateJob(queue, hook, failBookkeeping ? failingCache.Object : cache, CreateClientFactory(handler));
        await queue.EnqueueAsync(CreateNotification(hook));

        // Act
        var exception = await Record.ExceptionAsync(async () => { _ = await job.RunAsync(TestCancellationToken); });

        // Assert
        Assert.Equal(1, handler.RequestCount);
        Assert.False(content.WasSerialized);
        Assert.True(content.WasDisposed);
        Assert.Same(failBookkeeping ? failure : null, exception);
        Assert.Equal(failBookkeeping ? 1 : 0, Log.LogEntries.Count(entry => entry.LogLevel is LogLevel.Warning && entry.Message.Contains("BadGateway", StringComparison.Ordinal) && entry.Message.Contains("https://example.com", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ProcessQueueEntryAsync_TransportFailure_PreservesOriginalException()
    {
        // Arrange
        var failure = new HttpRequestException("Connection failed", new IOException("Socket closed"));
        using var handler = new DelegateHandler((_, _) => Task.FromException<HttpResponseMessage>(failure));
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        var hook = CreateWebHook(DestinationUrl);
        var job = CreateJob(queue, hook, cache, CreateClientFactory(handler));
        await queue.EnqueueAsync(CreateNotification(hook));

        // Act
        var result = await job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Same(failure, result.Error);
        var entry = Assert.Single(Log.LogEntries, entry => String.Equals(entry.EventId.Name, nameof(Exceptionless.Core.Extensions.LoggerExtensions.WebHookError), StringComparison.Ordinal));
        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public void WebHookLogging_Destination_OmitsCredentialsAndPreservesException()
    {
        // Arrange
        Log.DefaultLogLevel = LogLevel.Trace;
        const string url = "https://user:password@example.com/private-capability?secret-query=value";
        var exception = new HttpRequestException("Connection failed", new IOException("Socket closed"));

        // Act
        _logger.RecordWebHook(TestConstants.WebHookId, TestConstants.ProjectId, url);
        _logger.WebHookComplete(HttpStatusCode.OK, TestConstants.OrganizationId, TestConstants.ProjectId, url);
        _logger.WebHookError(HttpStatusCode.BadGateway, TestConstants.OrganizationId, TestConstants.ProjectId, url, exception);
        _logger.WebHookTimeout(null, TestConstants.OrganizationId, TestConstants.ProjectId, url, exception);
        _logger.WebHookDisabledStatusCode(TestConstants.WebHookId, HttpStatusCode.Gone, TestConstants.OrganizationId, TestConstants.ProjectId, url);

        // Assert
        var entries = Log.LogEntries.Where(entry => String.Equals(entry.CategoryName, typeof(WebHooksJobTests).FullName, StringComparison.Ordinal)).ToList();
        Assert.Equal(5, entries.Count);
        Assert.Equal(2, entries.Count(entry => ReferenceEquals(entry.Exception, exception)));
        Assert.All(entries, entry =>
        {
            var properties = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(entry.State).ToDictionary(property => property.Key, property => property.Value);
            Assert.Equal("https://example.com", properties["Destination"]);
            Assert.DoesNotContain("private-capability", entry.Message);
            Assert.DoesNotContain("secret-query", entry.Message);
            Assert.DoesNotContain("password", entry.Message);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessQueueEntryAsync_ExistingPrivateHook_DeliversWithSafeTelemetry(bool sampled)
    {
        // Arrange
        int delivered = 0;
        int redirected = 0;
        var cookies = new ConcurrentQueue<string>();
        var propagationHeaders = new ConcurrentQueue<string>();
        var internalHeaders = new ConcurrentQueue<(string TraceParent, string Baggage)>();
        var activities = new ConcurrentQueue<Activity>();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampled ? new AlwaysOnSampler() : new AlwaysOffSampler())
            .AddHttpClientInstrumentation(options => options.EnrichWithHttpRequestMessage = WebHookDelivery.EnrichActivity)
            .AddProcessor(new CaptureActivityProcessor(activities))
            .Build();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        server.MapPost("/private-capability", context =>
        {
            Interlocked.Increment(ref delivered);
            cookies.Enqueue(context.Request.Headers.Cookie.ToString());
            propagationHeaders.Enqueue(String.Concat(context.Request.Headers["traceparent"], context.Request.Headers["tracestate"], context.Request.Headers["baggage"]));
            context.Response.Headers.SetCookie = "receiver-session=private; Path=/";
            context.Response.StatusCode = 302;
            context.Response.Headers.Location = "/redirected-capability";
            return Task.CompletedTask;
        });
        server.Map("/redirected-capability", (HttpContext context) =>
        {
            propagationHeaders.Enqueue(String.Concat(context.Request.Headers["traceparent"], context.Request.Headers["tracestate"], context.Request.Headers["baggage"]));
            return Interlocked.Increment(ref redirected);
        });
        server.MapGet("/internal-metadata", (HttpContext context) =>
        {
            internalHeaders.Enqueue((context.Request.Headers["traceparent"].ToString(), context.Request.Headers["baggage"].ToString()));
            return Results.NoContent();
        });
        await server.StartAsync(TestCancellationToken);
        string address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var hook = CreateWebHook(address + "/private-capability?secret-query=value");
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        var factory = GetService<IHttpClientFactory>();
        var job = CreateJob(queue, hook, cache, factory);
        using var internalClient = factory.CreateClient();
        var notification = CreateNotification(hook);
        await queue.EnqueueAsync(notification);
        JobResult result;
        JobResult secondResult;
        HttpStatusCode internalStatus;
        var previousBaggage = Baggage.Current;
        Baggage.SetBaggage("webhook-test", "private-baggage");
        try
        {
            // Act
            result = await job.RunAsync(TestCancellationToken);
            await queue.EnqueueAsync(notification);
            secondResult = await job.RunAsync(TestCancellationToken);
            using var internalResponse = await internalClient.GetAsync(address + "/internal-metadata", TestCancellationToken);
            internalStatus = internalResponse.StatusCode;
        }
        finally
        {
            Baggage.Current = previousBaggage;
        }

        // Assert
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.True(secondResult.IsSuccess, secondResult.Error?.ToString());
        Assert.Equal(2, delivered);
        Assert.Equal(2, redirected);
        Assert.All(cookies, cookie => Assert.Empty(cookie));
        Assert.Equal(4, propagationHeaders.Count);
        Assert.All(propagationHeaders, header => Assert.Empty(header));
        int port = new Uri(address).Port;
        var clientActivities = activities.Where(activity => Equals(activity.GetTagItem("server.port"), port)).ToList();
        Assert.Equal(sampled ? 5 : 0, clientActivities.Count);
        var deliveries = clientActivities.Where(activity => String.Equals(activity.GetTagItem("url.full") as string, address, StringComparison.Ordinal)).ToList();
        Assert.Equal(sampled ? 4 : 0, deliveries.Count);
        Assert.Equal(sampled ? 2 : 0, deliveries.Count(activity => String.Equals(activity.GetTagItem("http.request.method") as string, "POST", StringComparison.Ordinal) && Equals(activity.GetTagItem("http.response.status_code"), 302)));
        Assert.Equal(sampled ? 2 : 0, deliveries.Count(activity => String.Equals(activity.GetTagItem("http.request.method") as string, "GET", StringComparison.Ordinal) && Equals(activity.GetTagItem("http.response.status_code"), 200)));
        Assert.All(deliveries, activity =>
        {
            Assert.True(activity.Duration > TimeSpan.Zero);
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Value?.ToString()?.Contains("capability", StringComparison.Ordinal) is true);
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Value?.ToString()?.Contains("secret-query", StringComparison.Ordinal) is true);
        });
        Assert.Equal(HttpStatusCode.NoContent, internalStatus);
        var headers = Assert.Single(internalHeaders);
        Assert.NotEmpty(headers.TraceParent);
        Assert.Contains("private-baggage", headers.Baggage);
        var internalActivities = clientActivities.Where(activity => activity.GetTagItem("url.full") is string url && url.StartsWith(address + "/internal-metadata", StringComparison.Ordinal)).ToList();
        Assert.Equal(sampled ? 1 : 0, internalActivities.Count);
        Assert.True(hook.IsEnabled);
        Assert.DoesNotContain(Log.LogEntries, entry => entry.Message.Contains("private-capability", StringComparison.Ordinal) || entry.Message.Contains("secret-query", StringComparison.Ordinal));
        await server.StopAsync(TestCancellationToken);
    }

    private WebHooksJob CreateJob(IQueue<WebHookNotification> queue, WebHook hook, ICacheClient cache, IHttpClientFactory factory)
    {
        var repository = new Mock<IWebHookRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByIdAsync(hook.Id, It.IsAny<CommandOptionsDescriptor<WebHook>>())).ReturnsAsync(hook);
        repository.Setup(value => value.MarkDisabledAsync(hook.Id)).Callback(() => hook.IsEnabled = false).Returns(Task.CompletedTask);
        return new WebHooksJob(queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository.Object,
            cache, GetService<ITextSerializer>(), GetService<JsonSerializerOptions>(), GetService<AppOptions>(), TimeProvider,
            GetService<IResiliencePolicyProvider>(), Log, factory);
    }

    private static WebHook CreateWebHook(string url) => new()
    {
        Id = TestConstants.WebHookId, OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId,
        Url = url, Version = WebHook.KnownVersions.Version2, EventTypes = [WebHook.KnownEventTypes.NewError]
    };

    private static WebHookNotification CreateNotification(WebHook hook) => new()
    {
        OrganizationId = hook.OrganizationId, ProjectId = hook.ProjectId, WebHookId = hook.Id,
        Type = WebHookType.General, Url = hook.Url, Data = new { synthetic = "delivery-test" }
    };

    private static IHttpClientFactory CreateClientFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory.Setup(value => value.CreateClient(WebHookDelivery.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));
        return factory.Object;
    }

    private sealed class CaptureActivityProcessor(ConcurrentQueue<Activity> activities) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity) => activities.Enqueue(activity);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return send(request, cancellationToken);
        }
    }

    private sealed class TrackingContent : HttpContent
    {
        public bool WasSerialized { get; private set; }
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasSerialized = true;
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
