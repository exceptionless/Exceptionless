using System.Net;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public sealed class WebHookDeliveryTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessQueueEntryAsync_Response_DisposesWithoutBufferingEvenWhenCompletionFails(bool failCompletion)
    {
        // Arrange
        var content = new TrackingContent();
        using var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var client = new HttpClient(handler, disposeHandler: false);
        var options = GetService<AppOptions>();
        var hook = new WebHook
        {
            Id = TestConstants.WebHookId, OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId,
            Url = "https://example.com/webhook", Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.NewError]
        };
        var repository = new Mock<IWebHookRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByIdAsync(hook.Id, It.IsAny<CommandOptionsDescriptor<WebHook>>())).ReturnsAsync(hook);
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        using var logs = new CaptureLoggerFactory { FailCompletion = failCompletion };
        using var job = new WebHooksJob(queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository.Object,
            cache, GetService<ITextSerializer>(), GetService<JsonSerializerOptions>(), options, TimeProvider,
            GetService<IResiliencePolicyProvider>(), logs, new SingleClientFactory(client));
        await queue.EnqueueAsync(new WebHookNotification
        {
            OrganizationId = hook.OrganizationId, ProjectId = hook.ProjectId, WebHookId = hook.Id,
            Type = WebHookType.General, Url = hook.Url, Data = new { synthetic = "delivery-test" }
        });

        // Act
        var exception = await Record.ExceptionAsync(async () => { _ = await job.RunAsync(TestCancellationToken); });

        // Assert
        Assert.Equal(1, handler.RequestCount);
        Assert.False(content.WasSerialized);
        Assert.True(content.WasDisposed);
        Assert.Equal(failCompletion ? "Completion logging failed" : null, exception?.Message);
    }

    [Fact]
    public async Task ProcessQueueEntryAsync_TransportFailure_PreservesOriginalException()
    {
        // Arrange
        var failure = new HttpRequestException("Connection failed", new IOException("Socket closed"));
        using var handler = new FailingHandler(failure);
        using var client = new HttpClient(handler, disposeHandler: false);
        var options = GetService<AppOptions>();
        var hook = new WebHook
        {
            Id = TestConstants.WebHookId, OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId,
            Url = "https://example.com/webhook", Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.NewError]
        };
        var repository = new Mock<IWebHookRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByIdAsync(hook.Id, It.IsAny<CommandOptionsDescriptor<WebHook>>())).ReturnsAsync(hook);
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        using var logs = new CaptureLoggerFactory();
        using var job = new WebHooksJob(queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository.Object,
            cache, GetService<ITextSerializer>(), GetService<JsonSerializerOptions>(), options, TimeProvider,
            GetService<IResiliencePolicyProvider>(), logs, new SingleClientFactory(client));
        await queue.EnqueueAsync(new WebHookNotification
        {
            OrganizationId = hook.OrganizationId, ProjectId = hook.ProjectId, WebHookId = hook.Id,
            Type = WebHookType.General, Url = hook.Url, Data = new { synthetic = "delivery-test" }
        });

        // Act
        var result = await job.RunAsync(TestCancellationToken);

        // Assert

        Assert.Same(failure, result.Error);
        Assert.Contains(logs.Exceptions, exception => ReferenceEquals(exception, failure));
    }

    [Fact]
    public async Task ProcessQueueEntryAsync_ExistingPrivateHook_DeliversAndFollowsRedirectWithoutOverrides()
    {
        // Arrange
        int delivered = 0;
        int redirected = 0;
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        server.MapPost("/private-capability", context =>
        {
            Interlocked.Increment(ref delivered);
            context.Response.StatusCode = 302;
            context.Response.Headers.Location = "/redirected-capability";
            return Task.CompletedTask;
        });
        server.Map("/redirected-capability", () => Interlocked.Increment(ref redirected));
        await server.StartAsync(TestCancellationToken);
        string address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var options = GetService<AppOptions>();
        var hook = new WebHook
        {
            Id = TestConstants.WebHookId, OrganizationId = TestConstants.OrganizationId, ProjectId = TestConstants.ProjectId,
            Url = address + "/private-capability?secret-query=value", Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.NewError]
        };
        var repository = new Mock<IWebHookRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByIdAsync(hook.Id, It.IsAny<CommandOptionsDescriptor<WebHook>>())).ReturnsAsync(hook);
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        using var logs = new CaptureLoggerFactory();
        using var job = new WebHooksJob(queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository.Object,
            cache, GetService<ITextSerializer>(), GetService<JsonSerializerOptions>(), options, TimeProvider,
            GetService<IResiliencePolicyProvider>(), logs, GetService<IHttpClientFactory>());
        await queue.EnqueueAsync(new WebHookNotification
        {
            OrganizationId = hook.OrganizationId, ProjectId = hook.ProjectId, WebHookId = hook.Id,
            Type = WebHookType.General, Url = hook.Url, Data = new { synthetic = "delivery-test" }
        });
        // Act
        var result = await job.RunAsync(TestCancellationToken);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(1, delivered);
        Assert.Equal(1, redirected);
        Assert.True(hook.IsEnabled);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-capability") || message.Contains("secret-query"));
        await server.StopAsync(TestCancellationToken);
    }

    [Fact]
    public void DeliveryLogging_RedactsDestinationAndPreservesException()
    {
        // Arrange
        using var capture = new CaptureLoggerFactory();
        const string url = "https://user:password@example.com/private-capability?secret-query=value";
        var exception = new HttpRequestException("Connection failed", new IOException("Socket closed"));
        // Act
        Exceptionless.Core.Extensions.LoggerExtensions.RecordWebHook(capture, "hook", "project", url);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookComplete(capture, HttpStatusCode.OK, "organization", "project", url);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookError(capture, HttpStatusCode.BadGateway, "organization", "project", url, exception);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookTimeout(capture, null, "organization", "project", url, exception);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookDisabledStatusCode(capture, "hook", HttpStatusCode.Gone, "organization", "project", url);

        // Assert
        Assert.Equal(5, capture.Messages.Count);
        Assert.Equal(2, capture.Exceptions.Count);
        Assert.All(capture.Exceptions, logged => Assert.Same(exception, logged));
        Assert.Contains(capture.Messages, message => message.Contains("Connection failed"));
        Assert.All(capture.Messages, message =>
        {
            Assert.DoesNotContain("private-capability", message);
            Assert.DoesNotContain("secret-query", message);
            Assert.DoesNotContain("password", message);
        });
    }

    private sealed class CaptureLoggerFactory : ILoggerFactory, ILogger
    {
        public bool FailCompletion { get; init; }
        public List<string> Messages { get; } = [];
        public List<Exception> Exceptions { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void AddProvider(ILoggerProvider provider) { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (FailCompletion && eventId.Id == 5)
                throw new InvalidOperationException("Completion logging failed");
            if (exception is not null)
                Exceptions.Add(exception);
            string values = state is IEnumerable<KeyValuePair<string, object?>> properties
                ? String.Join(" ", properties.Select(p => p.Value)) : String.Empty;
            Messages.Add(formatter(state, exception) + values + exception);
        }
        public void Dispose() { }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FailingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                response.Dispose();
            base.Dispose(disposing);
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
