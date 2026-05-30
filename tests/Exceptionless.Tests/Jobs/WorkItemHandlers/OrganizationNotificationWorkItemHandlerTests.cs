using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Tests.Mail;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.Extensions.DependencyInjection;
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
/// handler-level idempotency (per-org distributed lock + sent marker) ensures that stale
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

    private CountingMailer Mailer => GetService<CountingMailer>();
    private IMessagePublisher MessagePublisher => GetService<IMessagePublisher>();
    private IMessageSubscriber MessageSubscriber => GetService<IMessageSubscriber>();
    private ICacheClient CacheClient => GetService<ICacheClient>();
    private IResiliencePolicyProvider ResiliencePolicyProvider => GetService<IResiliencePolicyProvider>();

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);
        services.AddSingleton<CountingMailer>();
        services.ReplaceSingleton<IMailer>(sp => sp.GetRequiredService<CountingMailer>());
    }

    [Fact]
    public async Task RunAsync_WhenOnePlanOverageIsObservedBySixSubscribersWithoutQueueDuplicateDetection_ShouldEnqueueSixWorkItems()
    {
        // Arrange
        using var workItemQueue = CreateWorkItemQueue();
        await SubscribeToPlanOverageAsync(workItemQueue, subscriberCount: 6);

        // Act
        await MessagePublisher.PublishAsync(new PlanOverage { OrganizationId = PrimaryOrganizationId }, cancellationToken: TestContext.Current.CancellationToken);

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
        await MessagePublisher.PublishAsync(new PlanOverage { OrganizationId = QueueDuplicateDetectionOrganizationId }, cancellationToken: TestContext.Current.CancellationToken);

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
        var queueEntry = await workItemQueue.DequeueAsync(TestContext.Current.CancellationToken);
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
        await MessagePublisher.PublishAsync(new PlanOverage { OrganizationId = HourlyOrganizationId, IsHourly = true }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var queueEntry = await workItemQueue.DequeueAsync(TestContext.Current.CancellationToken);
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
        var uniqueIdentifier = workItem.UniqueIdentifier;

        // Assert
        Assert.Equal(OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: true), uniqueIdentifier);
    }

    [Fact]
    public void UniqueIdentifier_WhenHourlyNotificationIsCreated_ShouldMatchCanonicalNotificationKey()
    {
        // Arrange
        var workItem = CreateHourlyNotificationWorkItem(PrimaryOrganizationId);

        // Act
        var uniqueIdentifier = workItem.UniqueIdentifier;

        // Assert
        Assert.Equal(OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: false), uniqueIdentifier);
    }

    [Fact]
    public void GetNotificationKey_WhenMonthlyAndHourlyNotificationsAreCreated_ShouldUseDifferentKeys()
    {
        // Arrange
        // Act
        var monthlyKey = OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: true);
        var hourlyKey = OrganizationNotificationWorkItem.GetNotificationKey(PrimaryOrganizationId, isOverMonthlyLimit: false);

        // Assert
        Assert.Equal($"Organization:{PrimaryOrganizationId}:notification:monthly", monthlyKey);
        Assert.Equal($"Organization:{PrimaryOrganizationId}:notification:hourly", hourlyKey);
        Assert.NotEqual(monthlyKey, hourlyKey);
    }

    [Fact]
    public async Task HandleItemAsync_WhenLegacyHourlyThrottleProcessesStaleMonthlyDuplicates_ShouldSendOneEmailPerHourBucket()
    {
        // Arrange
        // Reproduce the pre-fix behavior: ThrottlingLockProvider(1/hour) allowed exactly one
        // item through per calendar-hour bucket. When a duplicate was abandoned and re-queued,
        // it could acquire a fresh lock once the next hour bucket opened — one email per hour.
        var organization = new Organization { Id = PrimaryOrganizationId, Name = "Acme Corp" };
        var user = new User
        {
            Id = "664ec4c1f12e4f2b7a0d2001",
            FullName = "Jane Smith",
            EmailAddress = "jane.smith@acmecorp.example",
            IsEmailAddressVerified = true,
            EmailNotificationsEnabled = true
        };

        var lockKey = OrganizationNotificationWorkItemHandler.GetNotificationLockKey(PrimaryOrganizationId, isOverMonthlyLimit: true);
        var lockProvider = new ThrottlingLockProvider(CacheClient, 1, TimeSpan.FromHours(1), TimeProvider, ResiliencePolicyProvider, GetService<ILoggerFactory>());

        Task ProcessDuplicateWithLegacyLockAsync()
        {
            return lockProvider.TryUsingAsync(lockKey, async () =>
            {
                await Mailer.SendOrganizationNoticeAsync(user, organization, isOverMonthlyLimit: true, isOverHourlyLimit: false);
            }, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);
        }

        // Act: each call simulates a stale duplicate item being dequeued in a new hour bucket
        await ProcessDuplicateWithLegacyLockAsync();                                   // Hour 0: lock acquired, email sent

        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
        await ProcessDuplicateWithLegacyLockAsync();                                   // Hour 1: new bucket, email sent again

        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
        await ProcessDuplicateWithLegacyLockAsync();                                   // Hour 2: new bucket, email sent again

        // Assert: the old code allowed one email per hour — this is the bug
        Assert.Equal(3, Mailer.OrganizationNoticeCount);
    }

    private async Task SubscribeToPlanOverageAsync(IQueue<WorkItemData> workItemQueue, int subscriberCount)
    {
        for (int i = 0; i < subscriberCount; i++)
        {
            var startupAction = new EnqueueOrganizationNotificationOnPlanOverage(workItemQueue, MessageSubscriber, GetService<ILoggerFactory>());
            await startupAction.RunAsync();
        }
    }

    private InMemoryQueue<WorkItemData> CreateWorkItemQueue(params IQueueBehavior<WorkItemData>[] behaviors)
    {
        var options = new InMemoryQueueOptions<WorkItemData>
        {
            Serializer = GetService<ISerializer>(),
            TimeProvider = TimeProvider,
            LoggerFactory = GetService<ILoggerFactory>()
        };

        if (behaviors.Length > 0)
            options.Behaviors = behaviors;

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
