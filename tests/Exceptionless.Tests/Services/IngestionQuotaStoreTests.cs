using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class IngestionQuotaStoreTests
{
    [Fact]
    public async Task ReserveAsync_ConcurrentCallers_NeverExceedAvailableCapacity()
    {
        var store = new InMemoryIngestionQuotaStore(new ProxyTimeProvider());

        Task<int>[] reservations = Enumerable.Range(0, 100)
            .Select(index => store.ReserveAsync(
                "organization-1",
                $"reservation-{index}",
                1,
                50,
                TimeSpan.FromMinutes(10),
                TestContext.Current.CancellationToken))
            .ToArray();

        int[] admitted = await Task.WhenAll(reservations);

        Assert.Equal(50, admitted.Sum());
        Assert.Equal(50, admitted.Count(count => count == 1));
    }

    [Fact]
    public async Task ReleaseAsync_IsIdempotentAndReturnsCapacity()
    {
        var store = new InMemoryIngestionQuotaStore(new ProxyTimeProvider());
        int first = await store.ReserveAsync(
            "organization-1",
            "reservation-1",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        await store.ReleaseAsync("organization-1", "reservation-1", TestContext.Current.CancellationToken);
        await store.ReleaseAsync("organization-1", "reservation-1", TestContext.Current.CancellationToken);
        int second = await store.ReserveAsync(
            "organization-1",
            "reservation-2",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(10, first);
        Assert.Equal(10, second);
    }

    [Fact]
    public async Task ReserveAsync_ExpiredLeaseReturnsCapacity()
    {
        var timeProvider = new ProxyTimeProvider();
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));
        var store = new InMemoryIngestionQuotaStore(timeProvider);
        int first = await store.ReserveAsync(
            "organization-1",
            "reservation-1",
            7,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);
        int second = await store.ReserveAsync(
            "organization-1",
            "reservation-2",
            7,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(11));
        int afterExpiration = await store.ReserveAsync(
            "organization-1",
            "reservation-3",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(7, first);
        Assert.Equal(3, second);
        Assert.Equal(10, afterExpiration);
    }

    [Fact]
    public async Task ReserveAsync_BucketRollsBeforeLeaseExpires_DoesNotReuseCapacity()
    {
        var timeProvider = new ProxyTimeProvider();
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));
        var store = new InMemoryIngestionQuotaStore(timeProvider);
        await store.ReserveAsync(
            "organization-1", "reservation-1", 10, 10,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        int nextBucket = await store.ReserveAsync(
            "organization-1", "reservation-2", 10, 10,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, nextBucket);
    }

    [Fact]
    public async Task ReserveAsync_PlanLimitChanges_PreservesOutstandingCapacity()
    {
        var store = new InMemoryIngestionQuotaStore(new ProxyTimeProvider());
        int first = await store.ReserveAsync(
            "organization-1", "reservation-1", 10, 10,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        int afterPlanIncrease = await store.ReserveAsync(
            "organization-1", "reservation-2", 20, 20,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        int afterPlanDecrease = await store.ReserveAsync(
            "organization-1", "reservation-3", 10, 5,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        Assert.Equal(10, first);
        Assert.Equal(10, afterPlanIncrease);
        Assert.Equal(0, afterPlanDecrease);
    }
}
