using System.Collections.Concurrent;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Insulation.Redis;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class RedisIngestionStackUsageStoreTests : IClassFixture<AppWebHostFactory>, IAsyncLifetime
{
    private readonly AppWebHostFactory _factory;
    private readonly ConcurrentBag<(string ProjectId, string EventId)> _eventIds = [];
    private readonly string _scope = $"redis-stack-usage-{Guid.NewGuid():N}";
    private IConnectionMultiplexer? _connection;

    public RedisIngestionStackUsageStoreTests(AppWebHostFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        string connectionString = await _factory.GetRedisConnectionStringAsync(TestContext.Current.CancellationToken);
        var configuration = ConfigurationOptions.Parse(connectionString);
        configuration.AbortOnConnectFail = false;
        configuration.CertificateValidation += static (_, _, _, _) => true;
        _connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        await WaitForRedisAsync(_connection, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
            return;

        RedisKey[] keys = RedisIngestionStackUsageStore.GetRegistryKeys(GetScopePrefix())
            .Concat(RedisIngestionStackUsageStore.GetRegistryReservationKeys(GetScopePrefix()))
            .Concat(RedisIngestionStackUsageStore.GetRegistryLeaseKeys(GetScopePrefix()))
            .Concat(RedisIngestionStackUsageStore.GetRegistryCounterKeys(GetScopePrefix()))
            .Concat(_eventIds
                .GroupBy(identity => identity.ProjectId, StringComparer.Ordinal)
                .SelectMany(group => RedisIngestionStackUsageStore.GetKeys(
                    GetScopePrefix(),
                    group.Key,
                    group.Select(identity => identity.EventId).Distinct(StringComparer.Ordinal))))
            .ToArray();
        await _connection.GetDatabase().KeyDeleteAsync(keys);
        await _connection.CloseAsync();
        _connection.Dispose();
    }

    [Fact]
    public async Task SettleAsync_CallerFailsAfterSettlement_RetryDoesNotDoubleCount()
    {
        var store = CreateStore();
        var usage = CreateUsage("event-a", new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var settled = await store.SettleAsync([usage], TestContext.Current.CancellationToken);
            Assert.Equal(1, Assert.Single(settled).Count);
            throw new InvalidOperationException("failure after the atomic settlement");
        });

        var retry = await store.SettleAsync([usage], TestContext.Current.CancellationToken);
        var pending = await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken);

        Assert.Empty(retry);
        Assert.Equal(1, Assert.Single(pending).Count);
    }

    [Fact]
    public async Task SettleAsync_RetryInvalidatesOlderEmptySnapshotFinalization()
    {
        var store = CreateStore();
        var usage = CreateUsage("event-a", new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc));
        RedisKey[] keys = RedisIngestionStackUsageStore.GetKeys(GetScopePrefix(), usage.ProjectId, [usage.EventId]);
        RedisKey[] registryKeys = RedisIngestionStackUsageStore.GetRegistryKeys(GetScopePrefix());
        RedisKey[] reservationKeys = RedisIngestionStackUsageStore.GetRegistryReservationKeys(GetScopePrefix());
        RedisKey[] leaseKeys = RedisIngestionStackUsageStore.GetRegistryLeaseKeys(GetScopePrefix());
        int registryShard = GetRegistryShard(usage.ProjectId);
        RedisKey registryKey = registryKeys[registryShard];
        RedisKey reservationKey = reservationKeys[registryShard];
        RedisKey leaseKey = leaseKeys[registryShard];
        string member = $"{usage.OrganizationId.Length}:{usage.OrganizationId}{usage.ProjectId.Length}:{usage.ProjectId}";
        const long staleLeaseToken = 42;
        long occurrence = new DateTimeOffset(usage.OccurrenceDateUtc).ToUnixTimeMilliseconds();
        IDatabase database = GetConnection().GetDatabase();

        // Recreate a worker that observed an empty aggregate and was paused before finalizing it,
        // followed by a settlement that committed but stopped before activating the registry.
        await Task.WhenAll(
            database.SortedSetAddAsync(registryKey, member, DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()),
            database.HashSetAsync(leaseKey, member, staleLeaseToken),
            database.SetAddAsync(keys[0], usage.StackId),
            database.HashSetAsync(keys[1], usage.StackId, 1),
            database.HashSetAsync(keys[2], usage.StackId, occurrence),
            database.HashSetAsync(keys[3], usage.StackId, occurrence),
            database.StringSetAsync(keys[^1], 1, TimeSpan.FromDays(7)));

        // The retry is fully deduplicated, but it must still invalidate the older worker's lease.
        Assert.Empty(await store.SettleAsync([usage], TestContext.Current.CancellationToken));

        const string staleFinalizeScript = """
            local currentLease = redis.call('HGET', KEYS[3], ARGV[1])
            if not currentLease or currentLease ~= ARGV[2] then
                return 0
            end
            redis.call('HDEL', KEYS[3], ARGV[1])
            redis.call('HDEL', KEYS[2], ARGV[1])
            return redis.call('ZREM', KEYS[1], ARGV[1])
            """;
        RedisResult staleFinalizeResult = await database.ScriptEvaluateAsync(
            staleFinalizeScript,
            [registryKey, reservationKey, leaseKey],
            [member, staleLeaseToken]);

        Assert.Equal(0, (long)staleFinalizeResult);
        var pending = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));
        Assert.Equal(1, pending.Count);
        Assert.Equal(usage.StackId, pending.StackId);
    }

    [Fact]
    public async Task SettleAsync_ConcurrentOverlappingBatches_CountsEachEventOnce()
    {
        var store = CreateStore();
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        var eventA = CreateUsage("event-a", occurrence);
        var eventB = CreateUsage("event-b", occurrence.AddSeconds(1));
        var eventC = CreateUsage("event-c", occurrence.AddSeconds(2));

        Task<IReadOnlyCollection<StackUsageSummary>>[] calls = Enumerable.Range(0, 40)
            .Select(index => store.SettleAsync(
                index % 2 == 0 ? [eventA, eventB] : [eventB, eventC],
                TestContext.Current.CancellationToken))
            .ToArray();
        var settled = await Task.WhenAll(calls);
        var pending = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        Assert.Equal(3, settled.SelectMany(result => result).Sum(result => result.Count));
        Assert.Equal(3, pending.Count);
        Assert.Equal(eventA.OccurrenceDateUtc, pending.MinimumOccurrenceDateUtc);
        Assert.Equal(eventC.OccurrenceDateUtc, pending.MaximumOccurrenceDateUtc);
    }

    [Fact]
    public async Task AcknowledgeAsync_NewUsageBehindClaim_RemainsPending()
    {
        var store = CreateStore();
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await store.SettleAsync([CreateUsage("event-a", occurrence)], TestContext.Current.CancellationToken);
        var taken = await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken);
        await store.SettleAsync([CreateUsage("event-b", occurrence.AddSeconds(1))], TestContext.Current.CancellationToken);

        await store.AcknowledgeAsync(taken, TestContext.Current.CancellationToken);
        var pending = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        Assert.Equal(1, pending.Count);
        Assert.Equal(occurrence.AddSeconds(1), pending.MinimumOccurrenceDateUtc);
        Assert.Equal(occurrence.AddSeconds(1), pending.MaximumOccurrenceDateUtc);
    }

    [Fact]
    public async Task SettleAsync_NotificationAlreadyCompleted_PreservesSharedStateBit()
    {
        var store = CreateStore();
        var usage = CreateUsage("event-a", new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc));
        RedisKey stateKey = RedisIngestionStackUsageStore.GetKeys(GetScopePrefix(), usage.ProjectId, [usage.EventId]).Last();
        await GetConnection().GetDatabase().StringSetAsync(stateKey, 2, TimeSpan.FromDays(7));

        var settled = await store.SettleAsync([usage], TestContext.Current.CancellationToken);
        RedisValue state = await GetConnection().GetDatabase().StringGetAsync(stateKey);

        Assert.Equal(1, Assert.Single(settled).Count);
        Assert.Equal(3, (int)state);
    }

    [Fact]
    public Task SettleAsync_ConcurrentAtomicNotificationCompletion_PreservesBothStateBits()
    {
        var store = CreateStore();
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        IngestionStackUsage[] usages = Enumerable.Range(0, 100)
            .Select(index => CreateUsage($"event-{index}", occurrence.AddMilliseconds(index)))
            .ToArray();
        IDatabase database = GetConnection().GetDatabase();

        return Task.WhenAll(usages.Select(async usage =>
        {
            RedisKey stateKey = RedisIngestionStackUsageStore.GetKeys(
                GetScopePrefix(),
                usage.ProjectId,
                [usage.EventId])[^1];
            await Task.WhenAll(
                store.SettleAsync([usage], TestContext.Current.CancellationToken),
                database.StringIncrementAsync(stateKey, 2));
            await database.KeyExpireAsync(stateKey, TimeSpan.FromDays(7));
            Assert.Equal(3, (int)await database.StringGetAsync(stateKey));
        }));
    }

    [Fact]
    public async Task ClaimPendingAsync_MultipleProjects_ReturnsEveryProjectPartition()
    {
        var store = CreateStore();
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await store.SettleAsync([CreateUsage("event-a", occurrence, "project-a")], TestContext.Current.CancellationToken);
        await store.SettleAsync([CreateUsage("event-b", occurrence.AddSeconds(1), "project-b")], TestContext.Current.CancellationToken);

        var pending = await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken);

        Assert.Equal(2, pending.Count);
        Assert.Equal(["project-a", "project-b"], pending.Select(usage => usage.ProjectId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ClaimPendingAsync_BusyFirstProject_DoesNotStarveLaterProject()
    {
        var store = CreateStore();
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        IngestionStackUsage[] busyProjectUsages = Enumerable.Range(0, 6)
            .Select(index => CreateUsage(
                $"event-a-{index}",
                occurrence.AddSeconds(index),
                "project-a",
                $"stack-a-{index}"))
            .ToArray();
        await store.SettleAsync(busyProjectUsages, TestContext.Current.CancellationToken);
        await store.SettleAsync(
            [CreateUsage("event-b", occurrence, "project-b", "stack-b")],
            TestContext.Current.CancellationToken);

        var pending = await store.ClaimPendingAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, usage => usage.ProjectId == "project-a");
        Assert.Contains(pending, usage => usage.ProjectId == "project-b");
    }

    [Fact]
    public async Task ClaimPendingAsync_CrashBeforeAcknowledge_ReusesSettlementIdentity()
    {
        var store = CreateStore(TimeSpan.FromMilliseconds(100));
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await store.SettleAsync([CreateUsage("event-a", occurrence)], TestContext.Current.CancellationToken);

        var first = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));
        Assert.Empty(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        var recovered = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        Assert.Equal(first.SettlementSequence, recovered.SettlementSequence);
        Assert.Equal(first.Count, recovered.Count);
        Assert.Equal(first.MinimumOccurrenceDateUtc, recovered.MinimumOccurrenceDateUtc);
        Assert.Equal(first.MaximumOccurrenceDateUtc, recovered.MaximumOccurrenceDateUtc);
    }

    [Fact]
    public async Task ClaimPendingAsync_CrashBeforeRegistryActivation_ReservationRecoversAggregate()
    {
        var store = CreateStore(TimeSpan.FromMilliseconds(100));
        const string organizationId = "organization";
        const string projectId = "project";
        const string stackId = "stack";
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        RedisKey[] aggregateKeys = RedisIngestionStackUsageStore.GetAggregateKeys(GetScopePrefix(), projectId);
        RedisKey registryKey = RedisIngestionStackUsageStore.GetRegistryKeys(GetScopePrefix())[0];
        RedisKey reservationKey = RedisIngestionStackUsageStore.GetRegistryReservationKeys(GetScopePrefix())[0];
        string member = $"{organizationId.Length}:{organizationId}{projectId.Length}:{projectId}";
        long reservationUntil = DateTimeOffset.UtcNow.AddMilliseconds(100).ToUnixTimeMilliseconds();
        IDatabase database = GetConnection().GetDatabase();

        // This is the state left if the atomic project settlement succeeds and the process stops
        // before activating its registry reservation.
        await Task.WhenAll(
            database.SetAddAsync(aggregateKeys[0], stackId),
            database.HashSetAsync(aggregateKeys[1], stackId, 1),
            database.HashSetAsync(aggregateKeys[2], stackId, new DateTimeOffset(occurrence).ToUnixTimeMilliseconds()),
            database.HashSetAsync(aggregateKeys[3], stackId, new DateTimeOffset(occurrence).ToUnixTimeMilliseconds()),
            database.SortedSetAddAsync(registryKey, member, reservationUntil),
            database.HashSetAsync(reservationKey, member, reservationUntil));

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        var recovered = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        Assert.Equal(organizationId, recovered.OrganizationId);
        Assert.Equal(projectId, recovered.ProjectId);
        Assert.Equal(stackId, recovered.StackId);
        Assert.Equal(1, recovered.Count);
    }

    [Fact]
    public async Task ClaimPendingAsync_ConcurrentStoreInstances_ClaimEachSettlementOnce()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 20; index++)
        {
            await firstStore.SettleAsync(
                [CreateUsage($"event-{index}", occurrence.AddSeconds(index), $"project-{index}", $"stack-{index}")],
                TestContext.Current.CancellationToken);
        }

        Task<IReadOnlyCollection<StackUsageClaim>>[] drainers = Enumerable.Range(0, 20)
            .Select(index => (index % 2 == 0 ? firstStore : secondStore)
                .ClaimPendingAsync(1, TestContext.Current.CancellationToken))
            .ToArray();
        StackUsageClaim[] claims = (await Task.WhenAll(drainers)).SelectMany(result => result).ToArray();

        Assert.Equal(20, claims.Length);
        Assert.Equal(20, claims.Select(claim => (claim.ProjectId, claim.StackId, claim.SettlementSequence)).Distinct().Count());
    }

    [Fact]
    public async Task ClaimPendingAsync_LargeRegistry_LeasesOnlyRequestedProjectCount()
    {
        var store = CreateStore(TimeSpan.FromMinutes(1));
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 100; index++)
        {
            await store.SettleAsync(
                [CreateUsage($"event-{index}", occurrence, $"project-{index}", $"stack-{index}")],
                TestContext.Current.CancellationToken);
        }

        Assert.Single(await store.ClaimPendingAsync(1, TestContext.Current.CancellationToken));

        double leaseThreshold = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds();
        SortedSetEntry[][] registryEntries = await Task.WhenAll(
            RedisIngestionStackUsageStore.GetRegistryKeys(GetScopePrefix())
                .Select(key => GetConnection().GetDatabase().SortedSetRangeByRankWithScoresAsync(key)));
        SortedSetEntry[] entries = registryEntries.SelectMany(value => value).ToArray();
        Assert.Equal(100, entries.Length);
        Assert.Single(entries, entry => entry.Score > leaseThreshold);
    }

    [Fact]
    public async Task SettleAsync_PendingAggregateOutlivesEventMarker()
    {
        var store = CreateStore();
        var usage = CreateUsage("event-a", new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc));
        await store.SettleAsync([usage], TestContext.Current.CancellationToken);

        RedisKey[] keys = RedisIngestionStackUsageStore.GetKeys(GetScopePrefix(), usage.ProjectId, [usage.EventId]);
        TimeSpan? aggregateTtl = await GetConnection().GetDatabase().KeyTimeToLiveAsync(keys[0]);
        TimeSpan? stateTtl = await GetConnection().GetDatabase().KeyTimeToLiveAsync(keys[^1]);

        Assert.NotNull(aggregateTtl);
        Assert.NotNull(stateTtl);
        Assert.True(aggregateTtl > stateTtl, $"Aggregate TTL {aggregateTtl} must exceed event-marker TTL {stateTtl}.");
    }

    [Fact]
    public async Task AcknowledgeAsync_StaleClaim_CannotRemoveNewerSettlement()
    {
        var store = CreateStore(TimeSpan.FromMilliseconds(100));
        DateTime occurrence = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        await store.SettleAsync([CreateUsage("event-a", occurrence)], TestContext.Current.CancellationToken);
        var first = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));
        await store.SettleAsync([CreateUsage("event-b", occurrence.AddSeconds(1))], TestContext.Current.CancellationToken);
        await store.AcknowledgeAsync([first], TestContext.Current.CancellationToken);
        var second = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        await store.AcknowledgeAsync([first], TestContext.Current.CancellationToken);
        Assert.Empty(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        var recovered = Assert.Single(await store.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

        Assert.Equal(second.SettlementSequence, recovered.SettlementSequence);
        Assert.True(second.SettlementSequence > first.SettlementSequence);
    }

    [Fact]
    public void GeneratedKeys_ProjectPartitions_CoLocateWithinProjectAndUseDifferentSlots()
    {
        RedisKey[] firstProjectKeys = RedisIngestionStackUsageStore.GetKeys(GetScopePrefix(), "project-a", ["event-a", "event-b"]);
        RedisKey[] secondProjectKeys = RedisIngestionStackUsageStore.GetKeys(GetScopePrefix(), "project-b", ["event-c", "event-d"]);
        int[] firstProjectSlots = firstProjectKeys.Select(GetRedisClusterSlot).ToArray();
        int[] secondProjectSlots = secondProjectKeys.Select(GetRedisClusterSlot).ToArray();

        Assert.Single(firstProjectSlots.Distinct());
        Assert.Single(secondProjectSlots.Distinct());
        Assert.NotEqual(firstProjectSlots[0], secondProjectSlots[0]);

        RedisKey[] registryKeys = RedisIngestionStackUsageStore.GetRegistryKeys(GetScopePrefix());
        RedisKey[] reservationKeys = RedisIngestionStackUsageStore.GetRegistryReservationKeys(GetScopePrefix());
        RedisKey[] leaseKeys = RedisIngestionStackUsageStore.GetRegistryLeaseKeys(GetScopePrefix());
        RedisKey[] counterKeys = RedisIngestionStackUsageStore.GetRegistryCounterKeys(GetScopePrefix());
        for (int index = 0; index < registryKeys.Length; index++)
        {
            Assert.Equal(GetRedisClusterSlot(registryKeys[index]), GetRedisClusterSlot(reservationKeys[index]));
            Assert.Equal(GetRedisClusterSlot(registryKeys[index]), GetRedisClusterSlot(leaseKeys[index]));
            Assert.Equal(GetRedisClusterSlot(registryKeys[index]), GetRedisClusterSlot(counterKeys[index]));
        }
    }

    private RedisIngestionStackUsageStore CreateStore(TimeSpan? claimLease = null)
    {
        var options = new AppOptions
        {
            EventIngestionV3 = new EventIngestionV3Options
            {
                IdempotencyWindow = TimeSpan.FromDays(7),
                StackUsageClaimLease = claimLease ?? TimeSpan.FromMinutes(1)
            }
        };
        return new RedisIngestionStackUsageStore(GetConnection(), options, _scope);
    }

    private IngestionStackUsage CreateUsage(
        string eventId,
        DateTime occurrenceDateUtc,
        string projectId = "project",
        string stackId = "stack")
    {
        string scopedEventId = String.Concat(_scope, "-", eventId);
        _eventIds.Add((projectId, scopedEventId));
        return new IngestionStackUsage(scopedEventId, "organization", projectId, stackId, occurrenceDateUtc);
    }

    private IConnectionMultiplexer GetConnection() =>
        _connection ?? throw new InvalidOperationException("Redis connection has not been initialized.");

    private string GetScopePrefix() => String.Concat(_scope, ":");

    private static int GetRegistryShard(string projectId)
    {
        uint hash = 2166136261;
        foreach (char character in projectId)
            hash = (hash ^ character) * 16777619;
        return (int)(hash % RedisIngestionStackUsageStore.GetRegistryKeys(String.Empty).Length);
    }

    private static int GetRedisClusterSlot(RedisKey key)
    {
        string value = key.ToString();
        int openingBrace = value.IndexOf('{');
        int closingBrace = openingBrace >= 0 ? value.IndexOf('}', openingBrace + 1) : -1;
        string hashValue = openingBrace >= 0 && closingBrace > openingBrace + 1
            ? value.Substring(openingBrace + 1, closingBrace - openingBrace - 1)
            : value;

        ushort crc = 0;
        foreach (byte item in Encoding.UTF8.GetBytes(hashValue))
        {
            crc ^= (ushort)(item << 8);
            for (int bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc % 16384;
    }

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
