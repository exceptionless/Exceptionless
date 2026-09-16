using Exceptionless.Core.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Insulation.Redis;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class RedisAtomicCacheBatchTests(AppWebHostFactory factory) : IAsyncLifetime, IClassFixture<AppWebHostFactory>
{
    private readonly string _scope = $"atomic-batch-{Guid.NewGuid():N}";
    private IConnectionMultiplexer _connection = null!;
    private RedisAtomicCacheBatch _batch = null!;

    public async ValueTask InitializeAsync()
    {
        _ = factory;
        _connection = await ConnectionMultiplexer.ConnectAsync(await AppWebHostFactory.GetConnectionStringAsync("Redis"));
        _batch = new RedisAtomicCacheBatch(_connection, new CacheOptions { Scope = _scope }, TimeProvider.System);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.GetDatabase().KeyDeleteAsync([
            Key("record"),
            Key("bucket"),
            Key("members"),
            Key("mismatch"),
            Key("mismatch-members"),
            Key("concurrent"),
            Key("wrong-type-scalar"),
            Key("wrong-type-list")
        ]);
        await _connection.CloseAsync();
        _connection.Dispose();
    }

    [Fact]
    public async Task TrySetAllAsync_ExpectedValuesMatch_UpdatesAllValuesWithIndependentExpirations()
    {
        var database = _connection.GetDatabase();
        await database.StringSetAsync(Key("record"), "__EX_MISSING__");
        await database.StringSetAsync(Key("bucket"), "1");
        var expected = new Dictionary<string, string?>
        {
            ["record"] = "__EX_MISSING__",
            ["bucket"] = "1"
        };
        var updates = new Dictionary<string, AtomicCacheValue>
        {
            ["record"] = new("active", TimeSpan.FromHours(1)),
            ["bucket"] = new("2", TimeSpan.FromMinutes(5))
        };

        bool updated = await _batch.TrySetAllAsync(
            expected,
            updates,
            new Dictionary<string, string> { ["members"] = "organization-id" },
            TimeSpan.FromMinutes(5));

        Assert.True(updated);
        Assert.Equal("active", await database.StringGetAsync(Key("record")));
        Assert.Equal("2", await database.StringGetAsync(Key("bucket")));
        Assert.NotNull(await database.SortedSetScoreAsync(Key("members"), "organization-id"));
        Assert.InRange((await database.KeyTimeToLiveAsync(Key("record")))!.Value.TotalMinutes, 59, 60);
        Assert.InRange((await database.KeyTimeToLiveAsync(Key("bucket")))!.Value.TotalMinutes, 4, 5);
    }

    [Fact]
    public async Task TrySetAllAsync_ExpectedValueDoesNotMatch_ChangesNothing()
    {
        var database = _connection.GetDatabase();
        await database.StringSetAsync(Key("mismatch"), "before");

        bool updated = await _batch.TrySetAllAsync(
            new Dictionary<string, string?> { ["mismatch"] = "different" },
            new Dictionary<string, AtomicCacheValue> { ["mismatch"] = new("after", TimeSpan.FromMinutes(5)) },
            new Dictionary<string, string> { ["mismatch-members"] = "member" },
            TimeSpan.FromMinutes(5));

        Assert.False(updated);
        Assert.Equal("before", await database.StringGetAsync(Key("mismatch")));
        Assert.Null(await database.SortedSetScoreAsync(Key("mismatch-members"), "member"));
    }

    [Fact]
    public async Task TrySetAllAsync_ConcurrentWriters_OnlyOneWins()
    {
        var database = _connection.GetDatabase();
        await database.StringSetAsync(Key("concurrent"), "before");
        var expected = new Dictionary<string, string?> { ["concurrent"] = "before" };

        var results = await Task.WhenAll(
            _batch.TrySetAllAsync(expected, new Dictionary<string, AtomicCacheValue> { ["concurrent"] = new("first", TimeSpan.FromMinutes(5)) }),
            _batch.TrySetAllAsync(expected, new Dictionary<string, AtomicCacheValue> { ["concurrent"] = new("second", TimeSpan.FromMinutes(5)) }));

        Assert.Single(results, result => result);
        Assert.Contains((string?)await database.StringGetAsync(Key("concurrent")), new[] { "first", "second" });
    }

    [Fact]
    public async Task TrySetAllAsync_ListKeyHasWrongType_DoesNotChangeScalars()
    {
        var database = _connection.GetDatabase();
        await database.StringSetAsync(Key("wrong-type-scalar"), "before");
        await database.StringSetAsync(Key("wrong-type-list"), "not-a-sorted-set");

        await Assert.ThrowsAsync<RedisServerException>(() => _batch.TrySetAllAsync(
            new Dictionary<string, string?> { ["wrong-type-scalar"] = "before" },
            new Dictionary<string, AtomicCacheValue> { ["wrong-type-scalar"] = new("after", TimeSpan.FromMinutes(5)) },
            new Dictionary<string, string> { ["wrong-type-list"] = "member" },
            TimeSpan.FromMinutes(5)));

        Assert.Equal("before", await database.StringGetAsync(Key("wrong-type-scalar")));
    }

    [Fact]
    public async Task TrySetAllAsync_InvalidBatch_RejectsBeforeRedisMutation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _batch.TrySetAllAsync(
            new Dictionary<string, string?> { ["expected"] = null },
            new Dictionary<string, AtomicCacheValue> { ["different"] = new("value", TimeSpan.FromMinutes(5)) }));

        await Assert.ThrowsAsync<ArgumentException>(() => _batch.TrySetAllAsync(
            new Dictionary<string, string?> { ["overlap"] = null },
            new Dictionary<string, AtomicCacheValue> { ["overlap"] = new("value", TimeSpan.FromMinutes(5)) },
            new Dictionary<string, string> { ["overlap"] = "member" },
            TimeSpan.FromMinutes(5)));
    }

    private RedisKey Key(string key) => $"{_scope}:{key}";
}
