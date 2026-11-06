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
using Microsoft.AspNetCore.Http;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Exceptionless.Tests.Jobs;

[CollectionDefinition("Webhook telemetry", DisableParallelization = true)]
public sealed class WebHookTelemetryCollection;

[Collection("Webhook telemetry")]
public sealed class WebHooksJobTests : TestWithServices
{
    private const string DestinationUrl = "https://example.com/webhook";
    private readonly IQueue<WebHookNotification> _queue;
    private readonly ICacheClient _cache;

    public WebHooksJobTests(ITestOutputHelper output) : base(output)
    {
        _queue = GetService<IQueue<WebHookNotification>>();
        _cache = GetService<ICacheClient>();
    }

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
        var payloads = new List<(string? ContentType, string Body)>();
        using var handler = new DelegateHandler(async (request, cancellationToken) =>
        {
            Assert.NotNull(request.Content);
            payloads.Add((request.Content.Headers.ContentType?.ToString(), await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status);
        });
        var hook = CreateWebHook(DestinationUrl);
        var job = CreateJob(hook, _cache, CreateClientFactory(handler));
        var notification = CreateNotification(hook);
        notification.Data = legacyPayload
            ? new VersionOnePlugin.VersionOneWebHookEvent(GetService<AppOptions>().BaseURL)
            {
                Id = TestConstants.EventId, Message = "<event>", ProjectId = hook.ProjectId, ProjectName = "Test project",
                OrganizationId = hook.OrganizationId, OrganizationName = "Test organization",
                ErrorStackId = TestConstants.StackId, ErrorStackTitle = "Test stack", ErrorStackTags = []
            }
            : new WebHookEvent(GetService<AppOptions>().BaseURL)
            {
                Id = TestConstants.EventId, Message = "<event>", ProjectId = hook.ProjectId, ProjectName = "Test project",
                OrganizationId = hook.OrganizationId, OrganizationName = "Test organization",
                StackId = TestConstants.StackId, StackTitle = "Test stack", StackTags = []
            };
        await _queue.EnqueueAsync(notification);

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
        Assert.Equal(errors, await _cache.GetAsync<long>($"Project:{hook.ProjectId}:webhook:{hook.Id}:errors", 0));
        Assert.Equal(1, (await _queue.GetQueueStatsAsync()).Completed);
        Assert.Equal(errors, Log.LogEntries.LongCount(entry => entry.LogLevel is LogLevel.Warning && String.Equals(entry.EventId.Name, nameof(Exceptionless.Core.Extensions.LoggerExtensions.WebHookUnsuccessfulResponse), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ProcessQueueEntryAsync_MissingWebHookId_LogsSafeDestinationWithoutSending()
    {
        // Arrange
        using var handler = new DelegateHandler((_, _) => throw new InvalidOperationException("Disabled notifications must not be sent"));
        var hook = CreateWebHook("https://user:password@example.com/private-capability?secret-query=value");
        var notification = CreateNotification(hook);
        notification.WebHookId = null;
        var job = CreateJob(hook, _cache, CreateClientFactory(handler));
        await _queue.EnqueueAsync(notification);

        // Act
        var result = await job.RunAsync(TestCancellationToken);

        // Assert
        Assert.True(result.IsCancelled);
        Assert.Equal(0, handler.RequestCount);
        var entry = Assert.Single(Log.LogEntries, entry => entry.LogLevel is LogLevel.Warning && String.Equals(entry.EventId.Name, nameof(Exceptionless.Core.Extensions.LoggerExtensions.WebHookMissingId), StringComparison.Ordinal));
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
        var failingCache = new Mock<ICacheClient>(MockBehavior.Strict);
        failingCache.Setup(client => client.GetAsync<long>(It.IsAny<string>())).Returns((string key) => _cache.GetAsync<long>(key));
        failingCache.Setup(client => client.SetAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan?>())).ThrowsAsync(failure);
        var hook = CreateWebHook(DestinationUrl);
        var job = CreateJob(hook, failBookkeeping ? failingCache.Object : _cache, CreateClientFactory(handler));
        await _queue.EnqueueAsync(CreateNotification(hook));

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
        var hook = CreateWebHook(DestinationUrl);
        var job = CreateJob(hook, _cache, CreateClientFactory(handler));
        await _queue.EnqueueAsync(CreateNotification(hook));

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
        _logger.WebHookUnsuccessfulResponse(HttpStatusCode.BadGateway, TestConstants.OrganizationId, TestConstants.ProjectId, url);
        _logger.WebHookError(HttpStatusCode.BadGateway, TestConstants.OrganizationId, TestConstants.ProjectId, url, exception);
        _logger.WebHookTimeout(null, TestConstants.OrganizationId, TestConstants.ProjectId, url, exception);
        _logger.WebHookDisabledStatusCode(TestConstants.WebHookId, HttpStatusCode.Gone, TestConstants.OrganizationId, TestConstants.ProjectId, url);

        // Assert
        var entries = Log.LogEntries.Where(entry => String.Equals(entry.CategoryName, typeof(WebHooksJobTests).FullName, StringComparison.Ordinal)).ToList();
        Assert.Equal(6, entries.Count);
        Assert.Equal(2, entries.Count(entry => ReferenceEquals(entry.Exception, exception)));
        Assert.All(entries.Where(entry => ReferenceEquals(entry.Exception, exception)), entry => Assert.Contains(exception.Message, entry.Message));
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
    public async Task ProcessQueueEntryAsync_GeneralWebHook_DeliversWithSafeTelemetry(bool sampled)
    {
        // Arrange
        var requests = new ConcurrentQueue<(string Path, string Cookie, string TraceParent, string TraceState, string Baggage)>();
        var activities = new ConcurrentQueue<Activity>();
        var sampledTraceIds = new ConcurrentDictionary<ActivityTraceId, bool>();
        // Parallel application hosts keep a parent-based HTTP tracing provider active in this process.
        using var applicationTracing = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
            .AddHttpClientInstrumentation(options => options.EnrichWithHttpRequestMessage = WebHookDelivery.EnrichActivity)
            .Build();
        using var tracing = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new TrackingSampler(sampled ? new AlwaysOnSampler() : new AlwaysOffSampler(), sampledTraceIds))
            .AddHttpClientInstrumentation(options => options.EnrichWithHttpRequestMessage = WebHookDelivery.EnrichActivity)
            .AddProcessor(new CaptureActivityProcessor(activities, sampledTraceIds))
            .Build();
        // Inspect outgoing diagnostics without including the receiver's request logs.
        Log.SetLogLevel("Microsoft.AspNetCore", LogLevel.Warning);
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ILoggerFactory>(Log);
        await using var server = builder.Build();
        server.Urls.Add("http://127.0.0.1:0");
        server.Use(async (context, next) =>
        {
            var headers = context.Request.Headers;
            requests.Enqueue((context.Request.Path.ToString(), headers.Cookie.ToString(), headers["traceparent"].ToString(), headers["tracestate"].ToString(), headers["baggage"].ToString()));
            await next(context);
        });
        server.MapPost("/private-capability", context =>
        {
            context.Response.Headers.SetCookie = "receiver-session=private; Path=/";
            context.Response.Redirect("/redirected-capability");
            return Task.CompletedTask;
        });
        server.MapGet("/redirected-capability", () => Results.Ok());
        server.MapGet("/internal-metadata", () => Results.NoContent());
        await server.StartAsync(TestCancellationToken);
        string address = server.Urls.Single();
        var hook = CreateWebHook(address + "/private-capability?secret-query=value");
        var factory = GetService<IHttpClientFactory>();
        var job = CreateJob(hook, _cache, factory);
        using var internalClient = factory.CreateClient();
        var notification = CreateNotification(hook);
        await _queue.EnqueueAsync(notification);
        JobResult result;
        JobResult secondResult;
        HttpStatusCode internalStatus;
        var previousBaggage = Baggage.Current;
        Baggage.SetBaggage("webhook-test", "private-baggage");
        using var parentActivity = new Activity("webhook-delivery-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        parentActivity.ActivityTraceFlags = sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
        try
        {
            // Act
            result = await job.RunAsync(TestCancellationToken);
            await _queue.EnqueueAsync(notification);
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
        var webHookRequests = requests.Where(request => !String.Equals(request.Path, "/internal-metadata", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, webHookRequests.Count);
        Assert.Equal(2, webHookRequests.Count(request => String.Equals(request.Path, "/private-capability", StringComparison.Ordinal)));
        Assert.Equal(2, webHookRequests.Count(request => String.Equals(request.Path, "/redirected-capability", StringComparison.Ordinal)));
        Assert.All(webHookRequests, request =>
        {
            Assert.Empty(request.Cookie);
            Assert.Empty(request.TraceParent);
            Assert.Empty(request.TraceState);
            Assert.Empty(request.Baggage);
        });
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
        var headers = Assert.Single(requests, request => String.Equals(request.Path, "/internal-metadata", StringComparison.Ordinal));
        Assert.NotEmpty(headers.TraceParent);
        Assert.Contains("private-baggage", headers.Baggage);
        var internalActivities = clientActivities.Where(activity => activity.GetTagItem("url.full") is string url && url.StartsWith(address + "/internal-metadata", StringComparison.Ordinal)).ToList();
        Assert.Equal(sampled ? 1 : 0, internalActivities.Count);
        Assert.True(hook.IsEnabled);
        Assert.DoesNotContain(Log.LogEntries, entry => entry.Message.Contains("private-capability", StringComparison.Ordinal) || entry.Message.Contains("secret-query", StringComparison.Ordinal));
        await server.StopAsync(TestCancellationToken);
    }

    private WebHooksJob CreateJob(WebHook hook, ICacheClient cache, IHttpClientFactory factory)
    {
        var repository = new Mock<IWebHookRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByIdAsync(hook.Id, It.IsAny<CommandOptionsDescriptor<WebHook>>())).ReturnsAsync(hook);
        repository.Setup(value => value.MarkDisabledAsync(hook.Id)).Callback(() => hook.IsEnabled = false).Returns(Task.CompletedTask);
        return new WebHooksJob(_queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository.Object,
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

    /// <summary>
    /// Captures only activities this tracer sampled. Integration test hosts in the same process register their own
    /// tracers, which record HttpClient activities that would otherwise reach this processor too.
    /// </summary>
    private sealed class CaptureActivityProcessor(ConcurrentQueue<Activity> activities, ConcurrentDictionary<ActivityTraceId, bool> sampledTraceIds) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            if (sampledTraceIds.ContainsKey(activity.TraceId))
                activities.Enqueue(activity);
        }
    }

    private sealed class TrackingSampler(Sampler inner, ConcurrentDictionary<ActivityTraceId, bool> sampledTraceIds) : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            var result = inner.ShouldSample(samplingParameters);
            if (result.Decision != SamplingDecision.Drop)
                sampledTraceIds.TryAdd(samplingParameters.TraceId, true);

            return result;
        }
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
