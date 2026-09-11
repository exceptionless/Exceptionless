using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class ConnectionLeaseStoreTests
{
    [Fact]
    public async Task TryAcquireAsync_ConcurrentConnections_AcquiresExactlyTheLimit()
    {
        var timeProvider = new ProxyTimeProvider();
        var store = new ConnectionLeaseStore(timeProvider);

        bool[] acquired = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(index => store.TryAcquireAsync("user", $"connection-{index}", 10, TimeSpan.FromMinutes(1))));

        Assert.Equal(10, acquired.Count(value => value));
    }

    [Fact]
    public async Task TryAcquireAsync_ExpiredLease_ReclaimsCapacityAfterReplicaLoss()
    {
        var timeProvider = new ProxyTimeProvider();
        var store = new ConnectionLeaseStore(timeProvider);

        Assert.True(await store.TryAcquireAsync("user", "lost-connection", 1, TimeSpan.FromMinutes(1)));
        Assert.False(await store.TryAcquireAsync("user", "blocked-connection", 1, TimeSpan.FromMinutes(1)));

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        Assert.True(await store.TryAcquireAsync("user", "replacement-connection", 1, TimeSpan.FromMinutes(1)));
        Assert.False(await store.RenewAsync("user", "lost-connection", TimeSpan.FromMinutes(1)));
    }
}
