using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Foundatio.Utility;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceRedisTests(ITestOutputHelper output, AuthServiceRedisTests.RedisFixture fixture)
    : TestWithServices(output), IClassFixture<AuthServiceRedisTests.RedisFixture>
{
    private const string EmailAddress = "user@exceptionless.test";
    private const string IpAddress = "192.0.2.1";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoginFailureCleanup_IndependentClientReusesObservedSlot_PreservesNewReservation(bool passwordRecovery)
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        await using var firstConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        await using var secondConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        using var firstCache = new RedisCacheClient(CreateCacheOptions(firstConnection));
        using var secondCache = new RedisCacheClient(CreateCacheOptions(secondConnection));
        string scope = $"auth-tests:{Guid.NewGuid():N}";
        var firstScopedCache = new TestScopedCacheClient(firstCache, scope, TimeProvider);
        var secondScopedCache = new TestScopedCacheClient(secondCache, scope, TimeProvider);
        var first = new AuthService(firstScopedCache, TimeProvider, Log.CreateLogger<AuthService>());
        var second = new AuthService(secondScopedCache, TimeProvider, Log.CreateLogger<AuthService>());

        try
        {
            await using var failed = await first.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
            Assert.NotNull(failed);
            await first.RecordLoginFailureAsync(failed);
            await using var success = passwordRecovery ? null : await first.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
            if (!passwordRecovery)
                Assert.NotNull(success);

            var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var continueRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            firstScopedCache.BeforeFailureRemoval = async () =>
            {
                observed.TrySetResult();
                await continueRemoval.Task.WaitAsync(TestTimeout, TestCancellationToken);
            };

            // Act
            Task completion = passwordRecovery
                ? first.ClearUserLoginAttemptsAsync(EmailAddress)
                : first.RecordLoginSuccessAsync(success!);
            AuthService.LoginAttempt? replacement = null;
            try
            {
                await observed.Task.WaitAsync(TestTimeout, TestCancellationToken);
                // Another instance clears the observed failure and admits a new owner before
                // the original cleanup reaches Redis's actual conditional delete.
                await second.ClearUserLoginAttemptsAsync(EmailAddress);
                replacement = await second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
            }
            finally
            {
                continueRemoval.TrySetResult();
                await completion.WaitAsync(TestTimeout, TestCancellationToken);
            }

            await using (replacement)
            {
                var remaining = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
                    second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken)));
                await DisposeAttemptsAsync(remaining);

                // Assert
                Assert.NotNull(replacement);
                Assert.Equal(4, remaining.Count(attempt => attempt is not null));
            }
        }
        finally
        {
            await firstScopedCache.RemoveAllAsync();
        }
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 15)]
    public async Task TryBeginLoginAsync_IndependentClients_EnforcesSharedPendingAndFailureLimits(bool sharedIp, int limit)
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        await using var firstConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        await using var secondConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        using var firstCache = new RedisCacheClient(CreateCacheOptions(firstConnection));
        using var secondCache = new RedisCacheClient(CreateCacheOptions(secondConnection));
        string scope = $"auth-tests:{Guid.NewGuid():N}";
        var firstScopedCache = new TestScopedCacheClient(firstCache, scope, TimeProvider);
        var secondScopedCache = new TestScopedCacheClient(secondCache, scope, TimeProvider);
        var first = new AuthService(firstScopedCache, TimeProvider, Log.CreateLogger<AuthService>());
        var second = new AuthService(secondScopedCache, TimeProvider, Log.CreateLogger<AuthService>());

        try
        {
            await using var initial = await first.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
            Assert.NotNull(initial);

            // Act
            var attempts = await Task.WhenAll(Enumerable.Range(1, limit).Select(index =>
                (index % 2 == 0 ? first : second).TryBeginLoginAsync(
                    sharedIp ? $"user{index}@exceptionless.test" : EmailAddress, IpAddress, TestCancellationToken)));
            await Task.WhenAll(attempts.Append(initial).Where(attempt => attempt is not null)
                .Select(attempt => second.RecordLoginFailureAsync(attempt!)));
            await DisposeAttemptsAsync(attempts);
            await using var denied = await first.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
            await first.ClearUserLoginAttemptsAsync(EmailAddress);
            await using var afterRecovery = await second.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
            await using var otherIp = await first.TryBeginLoginAsync(EmailAddress, "192.0.2.2", TestCancellationToken);

            // Assert
            Assert.Equal(limit - 1, attempts.Count(attempt => attempt is not null));
            Assert.Null(denied);
            Assert.Equal(!sharedIp, afterRecovery is not null);
            Assert.NotNull(otherIp);
        }
        finally
        {
            await firstScopedCache.RemoveAllAsync();
        }
    }

    private RedisCacheClientOptions CreateCacheOptions(IConnectionMultiplexer connection)
        => new()
        {
            ConnectionMultiplexer = connection,
            Serializer = GetService<ISerializer>(),
            TimeProvider = TimeProvider,
            ResiliencePolicyProvider = GetService<IResiliencePolicyProvider>(),
            LoggerFactory = Log
        };

    private static Task DisposeAttemptsAsync(IEnumerable<AuthService.LoginAttempt?> attempts)
        => Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => attempt!.DisposeAsync().AsTask()));

    private sealed class TestScopedCacheClient(ICacheClient cache, string scope, TimeProvider clock)
        : ScopedCacheClient(cache, scope), ICacheClient, IHaveTimeProvider
    {
        public Func<Task>? BeforeFailureRemoval { get; set; }

        // Redis 13.0.4 does not expose its configured clock through IHaveTimeProvider.
        // Absolute-expiration helpers must use the same controlled clock as AuthService.
        TimeProvider IHaveTimeProvider.TimeProvider => clock;

        async Task<bool> ICacheClient.RemoveIfEqualAsync<T>(string cacheKey, T expected)
        {
            if (BeforeFailureRemoval is not null && expected is string value && value.StartsWith("failed:", StringComparison.Ordinal))
                await BeforeFailureRemoval();

            return await base.RemoveIfEqualAsync(cacheKey, expected);
        }
    }

    public sealed class RedisFixture : IAsyncLifetime
    {
        private DistributedApplication? _app;

        public string ConnectionString { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Exceptionless_AppHost>(
                ["--test-redis", "--Logging:LogLevel:Default=Warning"], startup.Token);
            _app = await appHost.BuildAsync(startup.Token);
            try
            {
                await _app.StartAsync(startup.Token);
                await _app.ResourceNotifications.WaitForResourceHealthyAsync("Redis", startup.Token);
                ConnectionString = await _app.GetConnectionStringAsync("Redis", startup.Token)
                    ?? throw new InvalidOperationException("The Redis test resource did not provide a connection string.");
            }
            catch
            {
                await _app.DisposeAsync();
                _app = null;
                throw;
            }
        }

        public ValueTask DisposeAsync() => _app?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
