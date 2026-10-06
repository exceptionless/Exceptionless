using System.Net;
using System.Reflection;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public sealed class WebHookDeliveryTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public async Task ProcessQueueEntryAsync_SuccessfulDelivery_DoesNotBufferResponseBody()
    {
        var content = new TrackingContent();
        using var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var client = new HttpClient(handler, disposeHandler: false);
        var options = GetService<AppOptions>();
        var hook = new WebHook
        {
            Id = "222222222222222222222222", OrganizationId = "333333333333333333333333", ProjectId = "444444444444444444444444",
            Url = "https://example.com/webhook", Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.NewError]
        };
        var repository = DispatchProxy.Create<IWebHookRepository, HookRepositoryProxy>();
        ((HookRepositoryProxy)(object)repository).Hook = hook;
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        using var logs = new CaptureLoggerFactory();
        using var job = new WebHooksJob(queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository,
            cache, GetService<ITextSerializer>(), GetService<JsonSerializerOptions>(), options, TimeProvider,
            GetService<IResiliencePolicyProvider>(), logs, new SingleClientFactory(client), GetService<WebHookDestinationPolicy>());
        await queue.EnqueueAsync(new WebHookNotification
        {
            OrganizationId = hook.OrganizationId, ProjectId = hook.ProjectId, WebHookId = hook.Id,
            Type = WebHookType.General, Url = hook.Url, Data = new { synthetic = "delivery-test" }
        });

        await job.RunUntilEmptyAsync(TestCancellationToken);

        Assert.Equal(1, handler.RequestCount);
        Assert.False(content.WasSerialized);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessQueueEntryAsync_ExistingPrivateHook_RequiresExplicitOverrideAndDoesNotRedirect(bool allowPrivate)
    {
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
        options.WebHookOptions.AllowedPrivateNetworks = allowPrivate ? [IPNetwork.Parse("127.0.0.1/32")] : [];
        var policy = GetService<WebHookDestinationPolicy>();
        var hook = new WebHook
        {
            Id = "222222222222222222222222", OrganizationId = "333333333333333333333333", ProjectId = "444444444444444444444444",
            Url = address + "/private-capability?secret-query=value", Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.NewError]
        };
        var repository = DispatchProxy.Create<IWebHookRepository, HookRepositoryProxy>();
        ((HookRepositoryProxy)(object)repository).Hook = hook;
        using var queue = new InMemoryQueue<WebHookNotification>();
        using var cache = new InMemoryCacheClient();
        using var logs = new CaptureLoggerFactory();
        using var job = new WebHooksJob(queue, GetService<IProjectRepository>(), GetService<SlackService>(), repository,
            cache, GetService<ITextSerializer>(), GetService<JsonSerializerOptions>(), options, TimeProvider,
            GetService<IResiliencePolicyProvider>(), logs, GetService<IHttpClientFactory>(), policy);
        await queue.EnqueueAsync(new WebHookNotification
        {
            OrganizationId = hook.OrganizationId, ProjectId = hook.ProjectId, WebHookId = hook.Id,
            Type = WebHookType.General, Url = hook.Url, Data = new { synthetic = "delivery-test" }
        });
        await job.RunUntilEmptyAsync(TestCancellationToken);
        Assert.Equal(allowPrivate ? 1 : 0, delivered);
        Assert.Equal(0, redirected);
        Assert.True(hook.IsEnabled);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-capability") || message.Contains("secret-query"));
        await server.StopAsync(TestCancellationToken);
    }

    [Fact]
    public void DeliveryLogging_CapabilityInUrlAndException_OmitsSecretValues()
    {
        using var capture = new CaptureLoggerFactory();
        const string url = "https://user:password@example.com/private-capability?secret-query=value";
        var exception = new HttpRequestException(url);
        Exceptionless.Core.Extensions.LoggerExtensions.RecordWebHook(capture, "hook", "project", url);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookComplete(capture, HttpStatusCode.OK, "organization", "project", url);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookError(capture, HttpStatusCode.BadGateway, "organization", "project", url, exception);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookTimeout(capture, null, "organization", "project", url, exception);
        Exceptionless.Core.Extensions.LoggerExtensions.WebHookDisabledStatusCode(capture, "hook", HttpStatusCode.Gone, "organization", "project", url);
        Assert.Equal(5, capture.Messages.Count);
        Assert.All(capture.Messages, message =>
        {
            Assert.DoesNotContain("private-capability", message);
            Assert.DoesNotContain("secret-query", message);
            Assert.DoesNotContain("password", message);
        });
    }

    private class HookRepositoryProxy : DispatchProxy
    {
        public WebHook Hook { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal("GetByIdAsync", targetMethod!.Name);
            return Task.FromResult<WebHook?>(Hook);
        }
    }

    private sealed class CaptureLoggerFactory : ILoggerFactory, ILogger
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void AddProvider(ILoggerProvider provider) { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
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
