using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Tests.Extensions;
using Foundatio.AsyncEx;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

/// <summary>
/// RCA-focused tests for duplicate plan-limit email notifications.
///
/// Root cause: every web pod registers the same <see cref="EnqueueOrganizationNotificationOnPlanOverage"/>
/// startup action. Because Foundatio pub/sub delivers each message to all subscribers, a single
/// monthly <c>PlanOverage</c> event enqueued one work item per running web pod. The original
/// <c>ThrottlingLockProvider(slotsPerPeriod: 1, period: 1 hour)</c> allowed exactly one item
/// through per calendar-hour bucket. Duplicate items were abandoned back to the queue and
/// reprocessed once each new bucket opened — producing one email per hour.
///
/// Fix: queue-level dedup (<see cref="OrganizationNotificationWorkItem.UniqueIdentifier"/> +
/// <c>DuplicateDetectionQueueBehavior</c>) collapses the fanout at enqueue time, and
/// handler-level idempotency (per-organization distributed lock + sent marker) ensures that stale
/// duplicates already in the queue when the fix deployed cannot retrigger an email.
/// </summary>
public class OrganizationNotificationWorkItemHandlerTests : TestWithServices
{
    private const string PrimaryOrganizationId = "664ec4c1f12e4f2b7a0d1001";
    private const string QueueDuplicateDetectionOrganizationId = "664ec4c1f12e4f2b7a0d1002";
    private const string RegisteredQueueOrganizationId = "664ec4c1f12e4f2b7a0d1003";
    private const string DequeuedNotificationOrganizationId = "664ec4c1f12e4f2b7a0d1004";
    private const string HourlyOrganizationId = "664ec4c1f12e4f2b7a0d1005";

    public OrganizationNotificationWorkItemHandlerTests(ITestOutputHelper output) : base(output) { }

    private IMessagePublisher MessagePublisher => GetService<IMessagePublisher>();
    private IMessageSubscriber MessageSubscriber => GetService<IMessageSubscriber>();
    private ICacheClient CacheClient => GetService<ICacheClient>();

    [Fact]
    public async Task RunAsync_WhenOnePlanOverageIsObservedBySixSubscribersWithoutQueueDuplicateDetection_ShouldEnqueueSixWorkItems()
    {
        // Arrange
        using var workItemQueue = CreateWorkItemQueue();
        await SubscribeToPlanOverageAsync(workItemQueue, subscriberCount: 6);

        // Act
        await PublishPlanOverageAndWaitForQueueAsync(workItemQueue, new PlanOverage { OrganizationId = PrimaryOrganizationId }, expectedEnqueueAttempts: 6, expectedEnqueuedCount: 6);

        // Assert
        var queueStats = await workItemQueue.GetQueueStatsAsync();
        Assert.Equal(6, queueStats.Enqueued);
    }

    [Fact]
    public async Task RunAsync_WhenOnePlanOverageIsObservedBySixSubscribersWithQueueDuplicateDetection_ShouldEnqueueOneWorkItem()
    {
        // Arrange
        using var dedupBehavior = new DuplicateDetectionQueueBehavior<WorkItemData>(CacheClient, GetService<ILoggerFactory>(), TimeSpan.FromHours(24));
        using var workItemQueue = CreateWorkItemQueue(dedupBehavior);
        await SubscribeToPlanOverageAsync(workItemQueue, subscriberCount: 6);

        // Act
        await PublishPlanOverageAndWaitForQueueAsync(workItemQueue, new PlanOverage { OrganizationId = QueueDuplicateDetectionOrganizationId }, expectedEnqueueAttempts: 6, expectedEnqueuedCount: 1);

        // Assert
        var queueStats = await workItemQueue.GetQueueStatsAsync();
        Assert.Equal(1, queueStats.Enqueued);
    }

    [Fact]
    public async Task EnqueueAsync_WhenDuplicateNotificationUsesRegisteredQueueBehavior_ShouldEnqueueOneWorkItem()
    {
        // Arrange
        var workItemQueue = GetService<IQueue<WorkItemData>>();
        var workItem = CreateMonthlyNotificationWorkItem(RegisteredQueueOrganizationId);

        // Act
        await workItemQueue.EnqueueAsync(workItem);
        await workItemQueue.EnqueueAsync(workItem);

        // Assert
        var queueStats = await workItemQueue.GetQueueStatsAsync();
        Assert.Equal(1, queueStats.Enqueued);
    }

    [Fact]
    public async Task EnqueueAsync_WhenDuplicateNotificationIsDequeued_ShouldAllowFutureEnqueue()
    {
        // Arrange
        using var dedupBehavior = new DuplicateDetectionQueueBehavior<WorkItemData>(CacheClient, GetService<ILoggerFactory>(), TimeSpan.FromHours(24));
        using var workItemQueue = CreateWorkItemQueue(dedupBehavior);
        var workItem = CreateMonthlyNotificationWorkItem(DequeuedNotificationOrganizationId);

        await workItemQueue.EnqueueAsync(workItem);
        var queueEntry = await workItemQueue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(queueEntry);
        await queueEntry.CompleteAsync();

        // Act
        await workItemQueue.EnqueueAsync(workItem);

        // Assert
        var queueStats = await workItemQueue.GetQueueStatsAsync();
        Assert.Equal(2, queueStats.Enqueued);
    }

    [Fact]
    public async Task RunAsync_WhenHourlyPlanOverageIsObserved_ShouldEnqueueHourlyWorkItem()
    {
        // Arrange
        using var workItemQueue = CreateWorkItemQueue();
        await SubscribeToPlanOverageAsync(workItemQueue, subscriberCount: 1);

        // Act
        await PublishPlanOverageAndWaitForQueueAsync(workItemQueue, new PlanOverage { OrganizationId = HourlyOrganizationId, IsHourly = true }, expectedEnqueueAttempts: 1, expectedEnqueuedCount: 1);

        // Assert
        var queueEntry = await workItemQueue.DequeueAsync(TestCancellationToken);
        Assert.NotNull(queueEntry);

        var workItem = GetService<ISerializer>().Deserialize<OrganizationNotificationWorkItem>(queueEntry.Value.Data)!;
        Assert.Equal(HourlyOrganizationId, workItem.OrganizationId);
        Assert.True(workItem.IsOverHourlyLimit);
        Assert.False(workItem.IsOverMonthlyLimit);

        await queueEntry.CompleteAsync();
    }

    [Fact]
    public void UniqueIdentifier_WhenMonthlyNotificationIsCreated_ShouldMatchCanonicalNotificationKey()
    {
        // Arrange
        var workItem = CreateMonthlyNotificationWorkItem(PrimaryOrganizationId);

        // Act
        string uniqueIdentifier = workItem.UniqueIdentifier;

        // Assert
        Assert.Equal(OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: true), uniqueIdentifier);
    }

    [Fact]
    public void UniqueIdentifier_WhenHourlyNotificationIsCreated_ShouldMatchCanonicalNotificationKey()
    {
        // Arrange
        var workItem = CreateHourlyNotificationWorkItem(PrimaryOrganizationId);

        // Act
        string uniqueIdentifier = workItem.UniqueIdentifier;

        // Assert
        Assert.Equal(OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: false), uniqueIdentifier);
    }

    [Fact]
    public void GetNotificationKey_WhenMonthlyAndHourlyNotificationsAreCreated_ShouldUseDifferentKeys()
    {
        // Arrange
        // Act
        string monthlyKey = OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: true);
        string hourlyKey = OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: false);

        // Assert
        Assert.Equal($"Organization:{PrimaryOrganizationId}:notification:monthly", monthlyKey);
        Assert.Equal($"Organization:{PrimaryOrganizationId}:notification:hourly", hourlyKey);
        Assert.NotEqual(monthlyKey, hourlyKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetNotificationKey_WhenOrganizationIdIsNullOrEmpty_ShouldThrowArgumentException(string? organizationId)
    {
        // Arrange
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => OrganizationNotificationWorkItem.GetNotificationKey(organizationId!, isOverMonthlyLimit: true));
    }

    private async Task SubscribeToPlanOverageAsync(IQueue<WorkItemData> workItemQueue, int subscriberCount)
    {
        for (int i = 0; i < subscriberCount; i++)
        {
            var startupAction = new EnqueueOrganizationNotificationOnPlanOverage(workItemQueue, MessageSubscriber, GetService<ILoggerFactory>());
            await startupAction.RunAsync();
        }
    }

    private async Task PublishPlanOverageAndWaitForQueueAsync(IQueue<WorkItemData> workItemQueue, PlanOverage overage, int expectedEnqueueAttempts, int expectedEnqueuedCount)
    {
        var enqueueAttempts = new AsyncCountdownEvent(expectedEnqueueAttempts);
        var enqueued = new AsyncCountdownEvent(expectedEnqueuedCount);
        using var enqueueAttemptSubscription = workItemQueue.Enqueuing.AddHandler((_, _) =>
        {
            enqueueAttempts.Signal();
            return Task.CompletedTask;
        });
        using var enqueuedSubscription = workItemQueue.Enqueued.AddHandler((_, _) =>
        {
            enqueued.Signal();
            return Task.CompletedTask;
        });

        await MessagePublisher.PublishAsync(overage, cancellationToken: TestCancellationToken);
        await enqueueAttempts.WaitAsync(TimeSpan.FromSeconds(5));
        await enqueued.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private InMemoryQueue<WorkItemData> CreateWorkItemQueue(params IQueueBehavior<WorkItemData>[] behaviors)
    {
        var options = new InMemoryQueueOptions<WorkItemData>
        {
            Behaviors = behaviors,
            Serializer = GetService<ISerializer>(),
            TimeProvider = TimeProvider,
            ResiliencePolicyProvider = GetService<IResiliencePolicyProvider>(),
            LoggerFactory = GetService<ILoggerFactory>()
        };

        return new InMemoryQueue<WorkItemData>(options);
    }

    private static OrganizationNotificationWorkItem CreateMonthlyNotificationWorkItem(string organizationId)
    {
        return new OrganizationNotificationWorkItem
        {
            OrganizationId = organizationId,
            IsOverHourlyLimit = false,
            IsOverMonthlyLimit = true
        };
    }

    private static OrganizationNotificationWorkItem CreateHourlyNotificationWorkItem(string organizationId)
    {
        return new OrganizationNotificationWorkItem
        {
            OrganizationId = organizationId,
            IsOverHourlyLimit = true,
            IsOverMonthlyLimit = false
        };
    }
}
