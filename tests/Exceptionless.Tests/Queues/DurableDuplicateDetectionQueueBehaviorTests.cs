using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Foundatio.Caching;
using Foundatio.Queues;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Queues;

public sealed class DurableDuplicateDetectionQueueBehaviorTests : TestWithServices
{
    public DurableDuplicateDetectionQueueBehaviorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task EnqueueAsync_CompletedItemIsDequeued_DuplicateRemainsSuppressed()
    {
        var queue = GetService<IQueue<EventNotification>>();
        var notification = new EventNotification
        {
            EventId = Guid.NewGuid().ToString("N"),
            IsNew = true,
            IsRegression = false,
            TotalOccurrences = 1,
            DeduplicationId = Guid.NewGuid().ToString("N"),
            UseDurableDeduplication = true
        };

        await queue.EnqueueAsync(notification);
        var cache = GetService<ICacheClient>();
        var legacyClaim = await cache.GetAsync<bool>(notification.UniqueIdentifier);
        var durableClaim = await cache.GetAsync<string>(DurableDuplicateDetectionQueueBehavior<EventNotification>.GetCacheKey(notification.UniqueIdentifier));
        Assert.False(legacyClaim.HasValue);
        Assert.Equal("completed", durableClaim.Value);

        var entry = await queue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(entry);
        await entry.CompleteAsync();

        await queue.EnqueueAsync(notification);

        var statistics = await queue.GetQueueStatsAsync();
        Assert.Equal(1, statistics.Enqueued);
    }

    [Fact]
    public async Task EnqueueAsync_DurableWebHook_BypassesLegacyClaim()
    {
        var queue = GetService<IQueue<WebHookNotification>>();
        var notification = new WebHookNotification
        {
            OrganizationId = "organization-1",
            ProjectId = "project-1",
            WebHookId = "webhook-1",
            Type = WebHookType.General,
            Url = "https://example.com/webhook",
            Data = new { Message = "test" },
            DeduplicationId = "event-webhook:event-1:webhook-1:General",
            UseDurableDeduplication = true
        };

        await queue.EnqueueAsync(notification);

        var cache = GetService<ICacheClient>();
        var legacyClaim = await cache.GetAsync<bool>(notification.UniqueIdentifier);
        var durableClaim = await cache.GetAsync<string>(DurableDuplicateDetectionQueueBehavior<WebHookNotification>.GetCacheKey(notification.UniqueIdentifier));
        Assert.False(legacyClaim.HasValue);
        Assert.Equal("completed", durableClaim.Value);
    }

    [Fact]
    public async Task EnqueueAsync_DurableEnqueueFails_PendingExpiryAllowsRecoveryWithoutFalseCompletion()
    {
        var cache = GetService<ICacheClient>();
        var notification = new EventNotification
        {
            EventId = "event-1",
            IsNew = true,
            IsRegression = false,
            TotalOccurrences = 1,
            DeduplicationId = "event-notification:event-1",
            UseDurableDeduplication = true
        };
        var legacyBehavior = new ConditionalDuplicateDetectionQueueBehavior<EventNotification>(
            cache,
            GetService<ILoggerFactory>(),
            TimeSpan.FromDays(7));
        var durableBehavior = new DurableDuplicateDetectionQueueBehavior<EventNotification>(
            cache,
            GetService<ILoggerFactory>(),
            TimeSpan.FromDays(7));
        var failureBehavior = new OneShotEnqueuingFailureBehavior<EventNotification>();
        using var queue = CreateQueue(legacyBehavior, durableBehavior, failureBehavior);

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.EnqueueAsync(notification));

        string durableKey = DurableDuplicateDetectionQueueBehavior<EventNotification>.GetCacheKey(notification.UniqueIdentifier);
        var pendingClaim = await cache.GetAsync<string>(durableKey);
        var legacyClaim = await cache.GetAsync<bool>(notification.UniqueIdentifier);
        Assert.Equal("pending", pendingClaim.Value);
        Assert.False(legacyClaim.HasValue);
        Assert.Equal(0, (await queue.GetQueueStatsAsync()).Enqueued);

        var pendingException = await Assert.ThrowsAsync<InvalidOperationException>(() => queue.EnqueueAsync(notification));
        Assert.Contains("currently being enqueued", pendingException.Message, StringComparison.Ordinal);
        Assert.Equal("pending", (await cache.GetAsync<string>(durableKey)).Value);
        Assert.Equal(0, (await queue.GetQueueStatsAsync()).Enqueued);

        TimeProvider.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        await queue.EnqueueAsync(notification);

        Assert.Equal("completed", (await cache.GetAsync<string>(durableKey)).Value);
        Assert.Equal(1, (await queue.GetQueueStatsAsync()).Enqueued);
    }

    [Fact]
    public async Task EnqueueAsync_NonDurableItem_DoesNotSuppressDuplicateInstance()
    {
        var queue = GetService<IQueue<EventNotification>>();
        var notification = new EventNotification
        {
            EventId = Guid.NewGuid().ToString("N"),
            IsNew = true,
            IsRegression = false,
            TotalOccurrences = 1
        };

        await queue.EnqueueAsync(notification);
        var entry = await queue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(entry);
        await entry.CompleteAsync();
        await queue.EnqueueAsync(notification);

        var statistics = await queue.GetQueueStatsAsync();
        Assert.Equal(2, statistics.Enqueued);
    }

    [Fact]
    public async Task EnqueueAsync_NonDurableItem_SuppressesDuplicateWhileOriginalIsQueued()
    {
        var queue = GetService<IQueue<EventNotification>>();
        var notification = new EventNotification
        {
            EventId = Guid.NewGuid().ToString("N"),
            IsNew = true,
            IsRegression = false,
            TotalOccurrences = 1,
            DeduplicationId = Guid.NewGuid().ToString("N")
        };

        await queue.EnqueueAsync(notification);
        await queue.EnqueueAsync(notification);

        var statistics = await queue.GetQueueStatsAsync();
        Assert.Equal(1, statistics.Enqueued);
    }

    [Fact]
    public async Task EnqueueAsync_NonDurableWebHook_PreservesLegacyDequeueScopedDeduplication()
    {
        var queue = GetService<IQueue<WebHookNotification>>();
        var notification = new WebHookNotification
        {
            OrganizationId = "organization-1",
            ProjectId = "project-1",
            WebHookId = "webhook-1",
            Type = WebHookType.General,
            Url = "https://example.com/webhook",
            Data = new { Message = "test" },
            DeduplicationId = "event-webhook:event-1:webhook-1:General"
        };

        await queue.EnqueueAsync(notification);
        await queue.EnqueueAsync(notification);
        Assert.Equal(1, (await queue.GetQueueStatsAsync()).Enqueued);

        var entry = await queue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(entry);
        await entry.CompleteAsync();

        await queue.EnqueueAsync(notification);
        Assert.Equal(2, (await queue.GetQueueStatsAsync()).Enqueued);
    }

    private InMemoryQueue<EventNotification> CreateQueue(params IQueueBehavior<EventNotification>[] behaviors)
    {
        return new InMemoryQueue<EventNotification>(new InMemoryQueueOptions<EventNotification>
        {
            Behaviors = behaviors,
            Serializer = GetService<ISerializer>(),
            TimeProvider = TimeProvider,
            ResiliencePolicyProvider = GetService<IResiliencePolicyProvider>(),
            LoggerFactory = GetService<ILoggerFactory>()
        });
    }

    private sealed class OneShotEnqueuingFailureBehavior<T> : QueueBehaviorBase<T> where T : class
    {
        private int _shouldFail = 1;

        protected override Task OnEnqueuing(object sender, EnqueuingEventArgs<T> args)
        {
            if (Interlocked.Exchange(ref _shouldFail, 0) == 1)
                throw new InvalidOperationException("Injected enqueue failure.");

            return Task.CompletedTask;
        }
    }
}
