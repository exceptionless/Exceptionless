using System.Threading.RateLimiting;
using Exceptionless.Core;
using Exceptionless.Web.Endpoints;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class EventIngestionV3ConcurrencyLimiterTests
{
    [Fact]
    public void ReadFromConfiguration_IndependentStreamAndProcessingLimits_BindsEverySetting()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseURL"] = "http://localhost",
                ["EventIngestionV3:MaximumActiveStreams"] = "40",
                ["EventIngestionV3:ActiveStreamQueueLimit"] = "4",
                ["EventIngestionV3:MaximumActiveStreamsPerOrganization"] = "20",
                ["EventIngestionV3:ActiveStreamQueueLimitPerOrganization"] = "2",
                ["EventIngestionV3:MaximumConcurrentRequests"] = "6",
                ["EventIngestionV3:ConcurrencyQueueLimit"] = "8",
                ["EventIngestionV3:MaximumConcurrentRequestsPerOrganization"] = "3",
                ["EventIngestionV3:ConcurrencyQueueLimitPerOrganization"] = "4"
            })
            .Build();

        EventIngestionV3Options options = AppOptions.ReadFromConfiguration(configuration).EventIngestionV3;

        Assert.Equal(40, options.MaximumActiveStreams);
        Assert.Equal(4, options.ActiveStreamQueueLimit);
        Assert.Equal(20, options.MaximumActiveStreamsPerOrganization);
        Assert.Equal(2, options.ActiveStreamQueueLimitPerOrganization);
        Assert.Equal(6, options.MaximumConcurrentRequests);
        Assert.Equal(8, options.ConcurrencyQueueLimit);
        Assert.Equal(3, options.MaximumConcurrentRequestsPerOrganization);
        Assert.Equal(4, options.ConcurrencyQueueLimitPerOrganization);
    }

    [Fact]
    public async Task AcquireProcessingAsync_ManyOpenStreams_DoesNotConsumeProcessingPermits()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 20,
            maximumActiveStreamsPerOrganization: 20,
            maximumConcurrentRequests: 1,
            maximumConcurrentRequestsPerOrganization: 1);
        var streamLeases = new List<RateLimitLease>();

        try
        {
            for (int index = 0; index < 20; index++)
            {
                RateLimitLease globalStreamLease = await limiter.AcquireGlobalActiveStreamAsync(TestContext.Current.CancellationToken);
                Assert.True(globalStreamLease.IsAcquired);
                streamLeases.Add(globalStreamLease);

                RateLimitLease organizationStreamLease = await limiter.AcquireOrganizationActiveStreamAsync("organization-a", TestContext.Current.CancellationToken);
                Assert.True(organizationStreamLease.IsAcquired);
                streamLeases.Add(organizationStreamLease);
            }

            using RateLimitLease processingLease = await limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken);
            using RateLimitLease rejectedLease = await limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken);

            Assert.True(processingLease.IsAcquired);
            Assert.False(rejectedLease.IsAcquired);
        }
        finally
        {
            foreach (RateLimitLease streamLease in streamLeases)
                streamLease.Dispose();
        }
    }

    [Fact]
    public async Task AcquireActiveStreamsAsync_GlobalAndOrganizationLimits_AreIndependent()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 1,
            maximumActiveStreamsPerOrganization: 1,
            maximumConcurrentRequests: 1,
            maximumConcurrentRequestsPerOrganization: 1);

        using RateLimitLease globalLease = await limiter.AcquireGlobalActiveStreamAsync(TestContext.Current.CancellationToken);
        using RateLimitLease rejectedGlobalLease = await limiter.AcquireGlobalActiveStreamAsync(TestContext.Current.CancellationToken);
        using RateLimitLease firstOrganizationLease = await limiter.AcquireOrganizationActiveStreamAsync("organization-a", TestContext.Current.CancellationToken);
        using RateLimitLease rejectedOrganizationLease = await limiter.AcquireOrganizationActiveStreamAsync("organization-a", TestContext.Current.CancellationToken);
        using RateLimitLease secondOrganizationLease = await limiter.AcquireOrganizationActiveStreamAsync("organization-b", TestContext.Current.CancellationToken);

        Assert.True(globalLease.IsAcquired);
        Assert.False(rejectedGlobalLease.IsAcquired);
        Assert.True(firstOrganizationLease.IsAcquired);
        Assert.False(rejectedOrganizationLease.IsAcquired);
        Assert.True(secondOrganizationLease.IsAcquired);
    }

    [Fact]
    public async Task AcquireProcessingAsync_GlobalAndOrganizationLimits_AreBothEnforced()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 20,
            maximumActiveStreamsPerOrganization: 20,
            maximumConcurrentRequests: 2,
            maximumConcurrentRequestsPerOrganization: 1);

        using RateLimitLease firstOrganizationLease = await limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken);
        using RateLimitLease sameOrganizationLease = await limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken);
        using RateLimitLease secondOrganizationLease = await limiter.AcquireProcessingAsync("organization-b", TestContext.Current.CancellationToken);
        using RateLimitLease globalLimitLease = await limiter.AcquireProcessingAsync("organization-c", TestContext.Current.CancellationToken);

        Assert.True(firstOrganizationLease.IsAcquired);
        Assert.False(sameOrganizationLease.IsAcquired);
        Assert.True(secondOrganizationLease.IsAcquired);
        Assert.False(globalLimitLease.IsAcquired);
    }

    [Fact]
    public async Task AcquireProcessingAsync_QueueHasCapacity_WaitsUntilPermitIsReleased()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 20,
            maximumActiveStreamsPerOrganization: 20,
            maximumConcurrentRequests: 1,
            maximumConcurrentRequestsPerOrganization: 1,
            concurrencyQueueLimit: 1,
            concurrencyQueueLimitPerOrganization: 1);

        RateLimitLease firstLease = await limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken);
        Task<RateLimitLease> queuedLeaseTask = limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken).AsTask();
        Assert.False(queuedLeaseTask.IsCompleted);

        using RateLimitLease rejectedLease = await limiter.AcquireProcessingAsync("organization-a", TestContext.Current.CancellationToken);
        Assert.False(rejectedLease.IsAcquired);

        firstLease.Dispose();
        using RateLimitLease queuedLease = await queuedLeaseTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.True(queuedLease.IsAcquired);
    }

    [Fact]
    public async Task InvokeAsync_ActiveStreamLimitReached_ReturnsTooManyRequestsBeforeEndpoint()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 1,
            maximumActiveStreamsPerOrganization: 1,
            maximumConcurrentRequests: 1,
            maximumConcurrentRequestsPerOrganization: 1);
        using RateLimitLease heldLease = await limiter.AcquireGlobalActiveStreamAsync(TestContext.Current.CancellationToken);
        Assert.True(heldLease.IsAcquired);
        bool endpointInvoked = false;
        var middleware = new EventIngestionV3ActiveStreamMiddleware(_ =>
        {
            endpointInvoked = true;
            return Task.CompletedTask;
        });
        using ServiceProvider requestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(EventIngestionV3EndpointMetadata.Instance),
            "V3 event ingestion"));

        await middleware.InvokeAsync(context, limiter, CreateOptions(enabled: true));

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.False(endpointInvoked);
    }

    [Fact]
    public async Task InvokeAsync_NonIngestionEndpoint_BypassesActiveStreamLimit()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 1,
            maximumActiveStreamsPerOrganization: 1,
            maximumConcurrentRequests: 1,
            maximumConcurrentRequestsPerOrganization: 1);
        using RateLimitLease heldLease = await limiter.AcquireGlobalActiveStreamAsync(TestContext.Current.CancellationToken);
        Assert.True(heldLease.IsAcquired);
        bool endpointInvoked = false;
        var middleware = new EventIngestionV3ActiveStreamMiddleware(_ =>
        {
            endpointInvoked = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, limiter, CreateOptions(enabled: true));

        Assert.True(endpointInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_IngestionDisabled_BypassesActiveStreamLimit()
    {
        await using var limiter = CreateLimiter(
            maximumActiveStreams: 1,
            maximumActiveStreamsPerOrganization: 1,
            maximumConcurrentRequests: 1,
            maximumConcurrentRequestsPerOrganization: 1);
        using RateLimitLease heldLease = await limiter.AcquireGlobalActiveStreamAsync(TestContext.Current.CancellationToken);
        Assert.True(heldLease.IsAcquired);
        bool endpointInvoked = false;
        var middleware = new EventIngestionV3ActiveStreamMiddleware(_ =>
        {
            endpointInvoked = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(EventIngestionV3EndpointMetadata.Instance),
            "V3 event ingestion"));

        await middleware.InvokeAsync(context, limiter, CreateOptions(enabled: false));

        Assert.True(endpointInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static EventIngestionV3ConcurrencyLimiter CreateLimiter(
        int maximumActiveStreams,
        int maximumActiveStreamsPerOrganization,
        int maximumConcurrentRequests,
        int maximumConcurrentRequestsPerOrganization,
        int concurrencyQueueLimit = 0,
        int concurrencyQueueLimitPerOrganization = 0)
    {
        return new EventIngestionV3ConcurrencyLimiter(CreateOptions(
            true,
            maximumActiveStreams,
            maximumActiveStreamsPerOrganization,
            maximumConcurrentRequests,
            maximumConcurrentRequestsPerOrganization,
            concurrencyQueueLimit,
            concurrencyQueueLimitPerOrganization));
    }

    private static AppOptions CreateOptions(
        bool enabled,
        int maximumActiveStreams = 1,
        int maximumActiveStreamsPerOrganization = 1,
        int maximumConcurrentRequests = 1,
        int maximumConcurrentRequestsPerOrganization = 1,
        int concurrencyQueueLimit = 0,
        int concurrencyQueueLimitPerOrganization = 0)
    {
        return new AppOptions
        {
            EventIngestionV3 = new EventIngestionV3Options
            {
                Enabled = enabled,
                MaximumActiveStreams = maximumActiveStreams,
                MaximumActiveStreamsPerOrganization = maximumActiveStreamsPerOrganization,
                ActiveStreamQueueLimit = 0,
                ActiveStreamQueueLimitPerOrganization = 0,
                MaximumConcurrentRequests = maximumConcurrentRequests,
                MaximumConcurrentRequestsPerOrganization = maximumConcurrentRequestsPerOrganization,
                ConcurrencyQueueLimit = concurrencyQueueLimit,
                ConcurrencyQueueLimitPerOrganization = concurrencyQueueLimitPerOrganization
            }
        };
    }
}
