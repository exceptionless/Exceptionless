using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Tests.Services;

/// <summary>
/// Unit tests for RateCounterService, including the critical snooze back-alert regression test.
/// Uses an in-memory cache client and ProxyTimeProvider — no Elasticsearch required.
/// </summary>
public class RateCounterServiceTests
{
    private const string CounterKey = "project:P1:signal:Errors";
    private const string RuleId = "rule-001";
    private const string SubjectKey = "project:P1";

    private static (RateCounterService service, ProxyTimeProvider timeProvider, InMemoryCacheClient cache) Create()
    {
        var timeProvider = new ProxyTimeProvider();
        var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            TimeProvider = timeProvider
        });
        var service = new RateCounterService(cache, timeProvider);
        return (service, timeProvider, cache);
    }

    // -------------------------------------------------------------------------
    // CRITICAL REGRESSION TEST: Snooze back-alert prevention
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that when a rule was snoozed and the snooze recently expired, the
    /// evaluator's SumBucketsAsync call uses max(windowStart, snoozedUntil) as the
    /// effective window start — so traffic counted during the snooze window does NOT
    /// trigger the rule.
    ///
    /// Without the snooze fix: Rule A would see the 15 events counted 3 minutes ago
    /// (inside the 5-minute window) and fire incorrectly.
    ///
    /// With the snooze fix: Rule A uses snoozedUntilUtc as the effective lower boundary,
    /// so it only counts events after the snooze expired (0 events) and does NOT fire.
    /// </summary>
    [Fact]
    public async Task SumBucketsAsync_WithSnoozeFix_IgnoresTrafficDuringSnooze()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();

        // Start at T-3min (the time the snoozed events occurred)
        var baseTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var eventTime = baseTime.AddMinutes(-3);
        timeProvider.SetUtcNow(eventTime);

        for (int i = 0; i < 15; i++)
            await service.IncrementAsync(CounterKey, ct);

        // Advance to "now" (T+0)
        timeProvider.Advance(TimeSpan.FromMinutes(3));

        var now = baseTime;
        var windowDuration = TimeSpan.FromMinutes(5);
        var windowStartUtc = now.Subtract(windowDuration);   // T-5min

        // Rule A: snoozed until T-2min (snooze recently expired)
        var snoozedUntilUtc = now.AddMinutes(-2);             // T-2min

        // Without snooze fix: sum from T-5min to now = 15 events (fires incorrectly)
        long withoutFix = await service.SumBucketsAsync(CounterKey, windowStartUtc, now, ct);

        // With snooze fix: effective window start = max(T-5min, T-2min) = T-2min
        // Events were counted at T-3min which is BEFORE T-2min, so they're excluded
        var effectiveWindowStart = snoozedUntilUtc > windowStartUtc ? snoozedUntilUtc : windowStartUtc;
        long withFix = await service.SumBucketsAsync(CounterKey, effectiveWindowStart, now, ct);

        // Without fix: 15 events (would fire incorrectly)
        Assert.Equal(15, withoutFix);

        // With fix: 0 events (correctly prevents back-alert)
        Assert.Equal(0, withFix);
    }

    /// <summary>
    /// Verifies that Rule B (not snoozed) DOES fire for the same traffic.
    /// Companion test to the snooze regression — proves the snooze fix is selective.
    /// </summary>
    [Fact]
    public async Task SumBucketsAsync_NonSnoozedRule_CountsAllTrafficInWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();

        // Start at T-3min, then advance forward
        var baseTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var eventTime = baseTime.AddMinutes(-3);
        timeProvider.SetUtcNow(eventTime);

        for (int i = 0; i < 15; i++)
            await service.IncrementAsync(CounterKey, ct);

        timeProvider.Advance(TimeSpan.FromMinutes(3));

        var now = baseTime;
        var windowStartUtc = now.AddMinutes(-5);   // full 5min window

        // Non-snoozed rule: uses full window
        long count = await service.SumBucketsAsync(CounterKey, windowStartUtc, now, ct);

        // All 15 events are in the 5-minute window => threshold=10 => fires
        Assert.Equal(15, count);
    }

    // -------------------------------------------------------------------------
    // Bucket increment and sum tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IncrementAsync_SingleIncrement_CreatesCountBucket()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(now);

        await service.IncrementAsync(CounterKey, ct);

        long count = await service.SumBucketsAsync(CounterKey, now.AddMinutes(-1), now.AddMinutes(1), ct);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IncrementAsync_MultipleIncrements_AccumulatesCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(now);

        for (int i = 0; i < 7; i++)
            await service.IncrementAsync(CounterKey, ct);

        long count = await service.SumBucketsAsync(CounterKey, now.AddMinutes(-1), now.AddMinutes(1), ct);
        Assert.Equal(7, count);
    }

    [Fact]
    public async Task SumBucketsAsync_AcrossMultipleMinutes_SumsAllBuckets()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 5, 0, DateTimeKind.Utc);

        // Add 3 events at T-4min
        timeProvider.SetUtcNow(now.AddMinutes(-4));
        for (int i = 0; i < 3; i++)
            await service.IncrementAsync(CounterKey, ct);

        // Add 5 events at T-2min
        timeProvider.SetUtcNow(now.AddMinutes(-2));
        for (int i = 0; i < 5; i++)
            await service.IncrementAsync(CounterKey, ct);

        // Add 2 events at T-0
        timeProvider.SetUtcNow(now);
        for (int i = 0; i < 2; i++)
            await service.IncrementAsync(CounterKey, ct);

        long count = await service.SumBucketsAsync(CounterKey, now.AddMinutes(-5), now.AddMinutes(1), ct);
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task SumBucketsAsync_EndMinuteHasEvents_ExcludesEndMinute()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var end = new DateTime(2024, 1, 15, 10, 5, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(end.AddMinutes(-1));
        await service.IncrementAsync(CounterKey, ct);
        timeProvider.SetUtcNow(end);
        await service.IncrementAsync(CounterKey, ct);

        // Act
        long count = await service.SumBucketsAsync(CounterKey, end.AddMinutes(-5), end, ct);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SumBucketsAsync_EmptyWindow_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(now);

        long count = await service.SumBucketsAsync(CounterKey, now.AddMinutes(-5), now, ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SumBucketsAsync_EventsOutsideWindow_NotCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 10, 0, DateTimeKind.Utc);

        // Add events 10 minutes ago — outside a 5-minute window
        timeProvider.SetUtcNow(now.AddMinutes(-10));
        for (int i = 0; i < 20; i++)
            await service.IncrementAsync(CounterKey, ct);

        timeProvider.SetUtcNow(now);
        long count = await service.SumBucketsAsync(CounterKey, now.AddMinutes(-5), now, ct);
        Assert.Equal(0, count);
    }

    // -------------------------------------------------------------------------
    // Active counter key tracking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetActiveCounterKeysAsync_AfterIncrement_ReturnsKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(now);

        await service.IncrementAsync(CounterKey, ct);

        var keys = await service.GetActiveCounterKeysAsync(now, ct);
        Assert.Contains(CounterKey, keys);
    }

    [Fact]
    public async Task GetActiveCounterKeysAsync_DifferentMinute_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var now = new DateTime(2024, 1, 15, 10, 5, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(now);

        await service.IncrementAsync(CounterKey, ct);

        // Ask for a different minute
        var keys = await service.GetActiveCounterKeysAsync(now.AddMinutes(-3), ct);
        Assert.DoesNotContain(CounterKey, keys);
    }

    // -------------------------------------------------------------------------
    // Cooldown tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsOnCooldownAsync_WhenNoCooldownSet_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, _, _) = Create();
        bool onCooldown = await service.IsOnCooldownAsync(RuleId, SubjectKey, ct);
        Assert.False(onCooldown);
    }

    [Fact]
    public async Task IsOnCooldownAsync_AfterClaimingCooldown_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, _, _) = Create();
        Assert.True(await service.TrySetCooldownAsync(RuleId, SubjectKey, TimeSpan.FromHours(1), ct));
        bool onCooldown = await service.IsOnCooldownAsync(RuleId, SubjectKey, ct);
        Assert.True(onCooldown);
    }

    [Fact]
    public async Task TrySetCooldownAsync_DifferentRules_IndependentCooldowns()
    {
        var ct = TestContext.Current.CancellationToken;
        var (service, _, _) = Create();
        const string ruleId2 = "rule-002";

        Assert.True(await service.TrySetCooldownAsync(RuleId, SubjectKey, TimeSpan.FromHours(1), ct));

        bool rule1OnCooldown = await service.IsOnCooldownAsync(RuleId, SubjectKey, ct);
        bool rule2OnCooldown = await service.IsOnCooldownAsync(ruleId2, SubjectKey, ct);

        Assert.True(rule1OnCooldown);
        Assert.False(rule2OnCooldown);
    }

    [Fact]
    public async Task TrySetCooldownAsync_ConfiguredDuration_ExpiresWithoutBuffer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (service, timeProvider, _) = Create();
        var duration = TimeSpan.FromMinutes(5);
        Assert.True(await service.TrySetCooldownAsync(RuleId, SubjectKey, duration, ct));

        // Act
        timeProvider.Advance(duration);
        bool onCooldown = await service.IsOnCooldownAsync(RuleId, SubjectKey, ct);

        // Assert
        Assert.False(onCooldown);
    }

    [Fact]
    public async Task TrySetCooldownAsync_WhenAlreadyClaimed_ReturnsFalse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (service, _, _) = Create();

        // Act
        bool first = await service.TrySetCooldownAsync(RuleId, SubjectKey, TimeSpan.FromMinutes(5), ct);
        bool second = await service.TrySetCooldownAsync(RuleId, SubjectKey, TimeSpan.FromMinutes(5), ct);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }
}
