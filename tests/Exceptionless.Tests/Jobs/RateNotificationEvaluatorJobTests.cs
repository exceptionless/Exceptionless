using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class RateNotificationEvaluatorJobTests : IntegrationTestsBase
{
    private readonly RateNotificationEvaluatorJob _job;
    private readonly RateCounterService _counterService;
    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IQueue<RateNotification> _notificationQueue;

    public RateNotificationEvaluatorJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<RateNotificationEvaluatorJob>();
        _counterService = GetService<RateCounterService>();
        _ruleRepository = GetService<IRateNotificationRuleRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _notificationQueue = GetService<IQueue<RateNotification>>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Add(OrganizationExtensions.RateNotificationsFeature);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
    }

    private RateNotificationRule BuildRule(string? projectId = null, RateNotificationSignal signal = RateNotificationSignal.Errors, int threshold = 10, string? window = null, string? cooldown = null)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        return new RateNotificationRule
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = projectId ?? SampleDataService.TEST_PROJECT_ID,
            UserId = "507f1f77bcf86cd799439011",
            Name = "Test Rule",
            IsEnabled = true,
            Signal = signal,
            Subject = RateNotificationSubject.Project,
            Threshold = threshold,
            Window = window is not null ? TimeSpan.Parse(window) : TimeSpan.FromMinutes(5),
            Cooldown = cooldown is not null ? TimeSpan.Parse(cooldown) : TimeSpan.FromMinutes(10),
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        };
    }

    private string BuildCounterKey(RateNotificationRule rule) =>
        $"project:{rule.ProjectId}:signal:{rule.Signal}";

    [Fact]
    public async Task RunAsync_WhenThresholdCrossed_EnqueuesNotification()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: start at event time (2 min before "now"), then advance forward
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));

        var rule = await _ruleRepository.AddAsync(BuildRule(threshold: 5), o => o.ImmediateConsistency());
        string counterKey = BuildCounterKey(rule);

        // Simulate 10 events within the 5-minute window
        for (int i = 0; i < 10; i++)
            await _counterService.IncrementAsync(counterKey, ct);

        TimeProvider.Advance(TimeSpan.FromMinutes(2));

        // Act
        await _job.RunAsync(ct);

        // Assert — notification enqueued
        var stats = await _notificationQueue.GetQueueStatsAsync();
        Assert.True(stats.Enqueued > 0, "Expected a RateNotification to be enqueued when threshold is crossed.");
    }

    [Fact]
    public async Task RunAsync_WhenBelowThreshold_DoesNotEnqueue()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: start at event time, advance to "now"
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));

        var rule = await _ruleRepository.AddAsync(BuildRule(threshold: 50), o => o.ImmediateConsistency());
        string counterKey = BuildCounterKey(rule);

        // Only 5 events — well below threshold of 50
        for (int i = 0; i < 5; i++)
            await _counterService.IncrementAsync(counterKey, ct);

        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert — no new notification
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(queueBefore, queueAfter);
    }

    [Fact]
    public async Task RunAsync_NonPremiumOrganization_DoesNotEnqueue()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));
        var rule = await _ruleRepository.AddAsync(BuildRule(threshold: 1), o => o.ImmediateConsistency());
        await _counterService.IncrementAsync(BuildCounterKey(rule), ct);
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.HasPremiumFeatures = false;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());
        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(queueBefore, queueAfter);
    }

    [Fact]
    public async Task RunAsync_WhenRateNotificationsFeatureDisabled_DoesNotEnqueue()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));

        var rule = await _ruleRepository.AddAsync(BuildRule(threshold: 1), o => o.ImmediateConsistency());
        await _counterService.IncrementAsync(BuildCounterKey(rule), ct);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Remove(OrganizationExtensions.RateNotificationsFeature);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        await _job.RunAsync(ct);

        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(queueBefore, queueAfter);
    }

    [Fact]
    public async Task RunAsync_WhenEventsAreInCurrentMinute_DoesNotEvaluatePartialBucket()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2024, 6, 1, 12, 0, 30, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now);
        var rule = await _ruleRepository.AddAsync(BuildRule(threshold: 1), o => o.ImmediateConsistency());
        await _counterService.IncrementAsync(BuildCounterKey(rule), ct);
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(queueBefore, queueAfter);
    }

    [Fact]
    public async Task RunAsync_AfterSuccessfulRecovery_AdvancesCheckpointToLastCompleteMinute()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2024, 6, 1, 12, 0, 30, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now);
        await _counterService.SetLastEvaluatedMinuteAsync(now.AddMinutes(-4), ct);

        // Act
        var result = await _job.RunAsync(ct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2024, 6, 1, 11, 59, 0, DateTimeKind.Utc), await _counterService.GetLastEvaluatedMinuteAsync(ct));
    }

    [Fact]
    public async Task RunAsync_WhenOnCooldown_DoesNotEnqueueAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: start at event time, advance to "now", then set cooldown
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));

        var rule = await _ruleRepository.AddAsync(BuildRule(threshold: 5), o => o.ImmediateConsistency());
        string counterKey = BuildCounterKey(rule);
        string subjectKey = $"project:{rule.ProjectId}";

        // Add enough events to cross threshold
        for (int i = 0; i < 10; i++)
            await _counterService.IncrementAsync(counterKey, ct);

        TimeProvider.Advance(TimeSpan.FromMinutes(2));

        // Put rule on cooldown (at "now")
        Assert.True(await _counterService.TrySetCooldownAsync(rule.Id, subjectKey, TimeSpan.FromHours(1), ct));

        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert — still on cooldown, nothing extra enqueued
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(queueBefore, queueAfter);
    }

    [Fact]
    public async Task RunAsync_WhenActivelySnoozed_SkipsEvaluation()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: start at event time, advance to "now"
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));

        var ruleData = BuildRule(threshold: 5);
        ruleData.SnoozedUntilUtc = now.AddHours(2);  // snoozed for 2 more hours
        var rule = await _ruleRepository.AddAsync(ruleData, o => o.ImmediateConsistency());
        string counterKey = BuildCounterKey(rule);

        // Add events above threshold
        for (int i = 0; i < 20; i++)
            await _counterService.IncrementAsync(counterKey, ct);

        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert — snoozed rule does not fire
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(queueBefore, queueAfter);
    }

    /// <summary>
    /// CRITICAL REGRESSION: Snooze back-alert prevention at job level.
    ///
    /// Setup: Rule A was snoozed until T-2min (snooze recently expired).
    ///        15 events were counted at T-3min (during the snooze period).
    ///        Rule B (identical but not snoozed) gets the same traffic.
    ///
    /// Expected:
    ///   Rule B fires: full 5-min window sees 15 events at or above threshold.
    ///   Rule A does NOT fire: effective window is [T-2min, now], so 0 events (prevented).
    /// </summary>
    [Fact]
    public async Task RunAsync_SnoozeBackAlert_SnoozedRuleIgnoresTrafficDuringSnoozeWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        // Arrange: start at T-3min (events arrive during snooze), then advance to "now"
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-3));

        // Rule A: snoozed until T-2min (snooze has just expired at "now")
        var ruleAData = BuildRule(threshold: 10);
        ruleAData.SnoozedUntilUtc = now.AddMinutes(-2);
        var ruleA = await _ruleRepository.AddAsync(ruleAData, o => o.ImmediateConsistency());

        // Rule B: not snoozed — should fire normally
        var ruleB = await _ruleRepository.AddAsync(BuildRule(threshold: 10), o => o.ImmediateConsistency());

        // Both rules watch the same signal/counter key
        string counterKey = BuildCounterKey(ruleA);
        Assert.Equal(counterKey, BuildCounterKey(ruleB));

        // Simulate 15 events at T-3min (during Rule A's snooze window)
        for (int i = 0; i < 15; i++)
            await _counterService.IncrementAsync(counterKey, ct);

        TimeProvider.Advance(TimeSpan.FromMinutes(3));
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        long newNotifications = queueAfter - queueBefore;

        // Rule B should have fired (1 notification), Rule A should NOT have fired
        // So exactly 1 notification should be enqueued.
        Assert.Equal(1, newNotifications);
    }

    [Fact]
    public async Task RunAsync_MatchingRuleOnSecondPage_EnqueuesNotification()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(now.AddMinutes(-2));

        var rules = Enumerable.Range(0, 501).Select(index =>
        {
            var rule = BuildRule(
                signal: index == 500 ? RateNotificationSignal.Errors : RateNotificationSignal.AllEvents,
                threshold: index == 500 ? 1 : Int32.MaxValue);
            rule.Id = index.ToString("x24");
            rule.Name = $"Paged rule {index}";
            return rule;
        }).ToList();
        await _ruleRepository.AddAsync(rules, o => o.ImmediateConsistency());

        await _counterService.IncrementAsync($"project:{SampleDataService.TEST_PROJECT_ID}:signal:{RateNotificationSignal.Errors}", ct);
        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        long queueBefore = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;

        // Act
        await _job.RunAsync(ct);

        // Assert
        long queueAfter = (await _notificationQueue.GetQueueStatsAsync()).Enqueued;
        Assert.Equal(1, queueAfter - queueBefore);
    }
}
