using Exceptionless.Core;
using Exceptionless.Insulation.Redis;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class RedisConnectionLeaseStoreTests : IClassFixture<AppWebHostFactory>
{
    private readonly AppWebHostFactory _factory;

    public RedisConnectionLeaseStoreTests(AppWebHostFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TryAcquireAsync_TwoProvidersShareAtomicLimitAndRecoverExpiredLeases()
    {
        string connectionString = await _factory.GetRedisConnectionStringAsync(TestContext.Current.CancellationToken);
        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);
        string scope = $"lease-test-{Guid.NewGuid():N}";
        var first = new RedisConnectionLeaseStore(multiplexer, new AppOptions { AppScope = scope });
        var second = new RedisConnectionLeaseStore(multiplexer, new AppOptions { AppScope = scope });
        string[] connectionIds = Enumerable.Range(0, 40).Select(index => $"connection-{index}").ToArray();

        bool[] acquired = await Task.WhenAll(connectionIds.Select((connectionId, index) =>
            (index % 2 is 0 ? first : second).TryAcquireAsync("user", connectionId, 10, TimeSpan.FromMilliseconds(500))));

        Assert.Equal(10, acquired.Count(value => value));
        await Task.Delay(TimeSpan.FromMilliseconds(750), TestContext.Current.CancellationToken);
        Assert.True(await second.TryAcquireAsync("user", "replacement", 10, TimeSpan.FromSeconds(5)));
        Assert.False(await first.RenewAsync("user", connectionIds[Array.FindIndex(acquired, value => value)], TimeSpan.FromSeconds(5)));
        await second.ReleaseAsync("user", "replacement");
    }
}
