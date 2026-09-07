using System.Collections.Concurrent;
using Exceptionless.Insulation.Redis;
using Exceptionless.Tests.Utility;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class RedisIngestionQuotaStoreTests : IClassFixture<AppWebHostFactory>, IAsyncLifetime
{
    private readonly AppWebHostFactory _factory;
    private readonly ConcurrentBag<string> _organizationIds = [];
    private readonly string _scope = $"redis-quota-{Guid.NewGuid():N}";
    private IConnectionMultiplexer? _connection;

    public RedisIngestionQuotaStoreTests(AppWebHostFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        string connectionString = await _factory.GetRedisConnectionStringAsync(TestContext.Current.CancellationToken);
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.CertificateValidation += static (_, _, _, _) => true;
        _connection = await ConnectionMultiplexer.ConnectAsync(options);
        await WaitForRedisAsync(_connection, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
            return;

        RedisKey[] keys = _organizationIds
            .Distinct(StringComparer.Ordinal)
            .SelectMany(organizationId => RedisIngestionQuotaStore.GetKeys(GetScopePrefix(), organizationId))
            .ToArray();
        if (keys.Length > 0)
            await _connection.GetDatabase().KeyDeleteAsync(keys);

        await _connection.CloseAsync();
        _connection.Dispose();
    }

    [Fact]
    public async Task ReserveAsync_ConcurrentCallers_NeverExceedAvailableCapacity()
    {
        const string organizationId = "concurrent";
        var (store, _) = CreateStore(organizationId);
        Task<int>[] reservations = Enumerable.Range(0, 100)
            .Select(index => store.ReserveAsync(
                organizationId,
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
    public async Task ReserveAsync_SameReservationId_IsIdempotent()
    {
        const string organizationId = "idempotent";
        var (store, _) = CreateStore(organizationId);

        int first = await store.ReserveAsync(
            organizationId,
            "reservation-1",
            7,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);
        int replay = await store.ReserveAsync(
            organizationId,
            "reservation-1",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);
        int remainder = await store.ReserveAsync(
            organizationId,
            "reservation-2",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(7, first);
        Assert.Equal(7, replay);
        Assert.Equal(3, remainder);
    }

    [Fact]
    public async Task ReleaseAsync_IsIdempotentAndReturnsCapacity()
    {
        const string organizationId = "release";
        var (store, _) = CreateStore(organizationId);
        int first = await store.ReserveAsync(
            organizationId,
            "reservation-1",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        await store.ReleaseAsync(organizationId, "reservation-1", TestContext.Current.CancellationToken);
        await store.ReleaseAsync(organizationId, "reservation-1", TestContext.Current.CancellationToken);
        int second = await store.ReserveAsync(
            organizationId,
            "reservation-2",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(10, first);
        Assert.Equal(10, second);
    }

    [Fact]
    public async Task ReserveAsync_ExpiredLeasesReturnCapacity()
    {
        const string organizationId = "expiration";
        var (store, timeProvider) = CreateStore(organizationId);
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));
        int first = await store.ReserveAsync(
            organizationId,
            "reservation-1",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(11));
        int afterExpiration = await store.ReserveAsync(
            organizationId,
            "reservation-2",
            10,
            10,
            TimeSpan.FromMinutes(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(10, first);
        Assert.Equal(10, afterExpiration);
    }

    [Fact]
    public void GeneratedKeys_ShareRedisClusterHashSlot()
    {
        const string organizationId = "cluster-slot";
        _organizationIds.Add(organizationId);
        RedisKey[] keys = RedisIngestionQuotaStore.GetKeys(GetScopePrefix(), organizationId);
        int[] slots = keys.Select(GetConnection().GetHashSlot).ToArray();

        Assert.Single(slots.Distinct());
    }

    [Fact]
    public async Task ReserveAsync_BucketRollsBeforeLeaseExpires_DoesNotReuseCapacity()
    {
        const string organizationId = "bucket-rollover";
        var (store, timeProvider) = CreateStore(organizationId);
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));
        await store.ReserveAsync(
            organizationId, "reservation-1", 10, 10,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        int nextBucket = await store.ReserveAsync(
            organizationId, "reservation-2", 10, 10,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, nextBucket);
    }

    [Fact]
    public async Task ReserveAsync_PlanLimitChanges_PreservesOutstandingCapacity()
    {
        const string organizationId = "plan-change";
        var (store, _) = CreateStore(organizationId);
        int first = await store.ReserveAsync(
            organizationId, "reservation-1", 10, 10,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        int afterPlanIncrease = await store.ReserveAsync(
            organizationId, "reservation-2", 20, 20,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        int afterPlanDecrease = await store.ReserveAsync(
            organizationId, "reservation-3", 10, 5,
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        Assert.Equal(10, first);
        Assert.Equal(10, afterPlanIncrease);
        Assert.Equal(0, afterPlanDecrease);
    }

    private (RedisIngestionQuotaStore Store, ProxyTimeProvider TimeProvider) CreateStore(string organizationId)
    {
        _organizationIds.Add(organizationId);
        var timeProvider = new ProxyTimeProvider();
        return (new RedisIngestionQuotaStore(GetConnection(), timeProvider, _scope), timeProvider);
    }

    private IConnectionMultiplexer GetConnection() => _connection ?? throw new InvalidOperationException("Redis connection has not been initialized.");
    private string GetScopePrefix() => String.Concat(_scope, ":");

    private static async Task WaitForRedisAsync(IConnectionMultiplexer connection, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = TimeProvider.System.GetUtcNow().AddSeconds(30);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await connection.GetDatabase().PingAsync();
                return;
            }
            catch (RedisConnectionException)
            {
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("LOADING", StringComparison.Ordinal))
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for the shared Redis container to accept connections.");
    }
}
