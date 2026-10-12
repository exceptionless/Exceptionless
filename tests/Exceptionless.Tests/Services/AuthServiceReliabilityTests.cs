using Exceptionless.Core.Services;
using Foundatio.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceReliabilityTests
{
    private const string EmailAddress = "user@exceptionless.test";
    private const string IpAddress = "192.0.2.1";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DisposeAsync_RepeatedDisposal_PerformsCleanupOnce()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        using var cache = new InstrumentedCacheClient(timeProvider);
        var service = CreateService(cache, timeProvider);
        var attempt = await BeginAsync(service);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => attempt.DisposeAsync().AsTask()));

        // Assert
        Assert.Equal(2, cache.ReadOperations);
        Assert.Equal(2, cache.Removals);
        Assert.Empty(cache.Keys);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public async Task RecordLoginFailureAsync_FailedOutcomeWrite_RetainsBothBudgets(bool synchronousFailure, bool commitBeforeFailure, bool failUserOnly)
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        using var cache = new InstrumentedCacheClient(timeProvider)
        {
            FailOutcomeWrites = true,
            SynchronousFailure = synchronousFailure,
            CommitBeforeFailure = commitBeforeFailure,
            FailUserOnly = failUserOnly
        };
        var service = CreateService(cache, timeProvider);
        var attempt = await BeginAsync(service);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using (attempt)
                await service.RecordLoginFailureAsync(attempt);
        });
        await attempt.DisposeAsync();
        int cleanupRemovals = cache.Removals;
        cache.FailOutcomeWrites = false;
        var userAttempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            service.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken)));
        var ipAttempts = await Task.WhenAll(Enumerable.Range(0, 15).Select(index =>
            service.TryBeginLoginAsync($"other{index}@exceptionless.test", IpAddress, TestCancellationToken)));
        await DisposeAttemptsAsync(userAttempts.Concat(ipAttempts));
        timeProvider.Advance(TimeSpan.FromMinutes(15));
        var afterExpiration = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken)));
        await DisposeAttemptsAsync(afterExpiration);

        // Assert
        Assert.Same(cache.Failure, exception);
        Assert.Equal(0, cleanupRemovals);
        Assert.Equal(4, userAttempts.Count(value => value is not null));
        Assert.Equal(14, ipAttempts.Count(value => value is not null));
        Assert.All(afterExpiration, Assert.NotNull);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_AlreadyFailedAttempt_PreservesEarlierFailures()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        using var cache = new InstrumentedCacheClient(timeProvider);
        var service = CreateService(cache, timeProvider);
        await using var previous = await BeginAsync(service);
        await service.RecordLoginFailureAsync(previous);
        await using var failed = await BeginAsync(service);
        await service.RecordLoginFailureAsync(failed);

        // Act
        await service.RecordLoginSuccessAsync(failed);
        var remaining = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            service.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken)));
        await DisposeAttemptsAsync(remaining);

        // Assert
        Assert.Equal(3, remaining.Count(value => value is not null));
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_RepeatedCompletion_DoesNotRepeatCacheOperations()
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        using var cache = new InstrumentedCacheClient(timeProvider);
        var service = CreateService(cache, timeProvider);
        var attempt = await BeginAsync(service);

        // Act
        await service.RecordLoginSuccessAsync(attempt);
        await service.RecordLoginSuccessAsync(attempt);
        await service.RecordLoginFailureAsync(attempt);
        await attempt.DisposeAsync();
        await attempt.DisposeAsync();

        // Assert
        Assert.Equal(2, cache.ReadOperations);
        Assert.Equal(2, cache.Removals);
        Assert.Empty(cache.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordLoginSuccessAsync_SharedCacheWrappers_SerializesObservedRemovalAndNewAdmission(bool nestedScope)
    {
        // Arrange
        var timeProvider = CreateTimeProvider();
        using var cache = new InstrumentedCacheClient(timeProvider);
        ICacheClient firstCache = new ScopedCacheClient(cache, "test");
        ICacheClient secondCache = new ScopedCacheClient(cache, "test");
        if (nestedScope)
        {
            firstCache = new ScopedCacheClient(firstCache, "nested");
            secondCache = new ScopedCacheClient(secondCache, "nested");
        }

        var first = CreateService(firstCache, timeProvider);
        var second = CreateService(secondCache, timeProvider);
        await using var failed = await BeginAsync(first);
        await first.RecordLoginFailureAsync(failed);
        await using var success = await BeginAsync(first);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.AfterRead = async value =>
        {
            if (!value.StartsWith("failed:", StringComparison.Ordinal))
                return;

            observed.TrySetResult();
            await continueRemoval.Task.WaitAsync(TestTimeout, TestCancellationToken);
        };

        // Act
        Task completion = first.RecordLoginSuccessAsync(success);
        Task<AuthService.LoginAttempt?> acquisition;
        bool admissionWaited;
        try
        {
            await observed.Task.WaitAsync(TestTimeout, TestCancellationToken);
            acquisition = second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
            admissionWaited = !acquisition.IsCompleted;
        }
        finally
        {
            continueRemoval.TrySetResult();
        }

        await completion.WaitAsync(TestTimeout, TestCancellationToken);
        await using var concurrent = await acquisition.WaitAsync(TestTimeout, TestCancellationToken);
        cache.AfterRead = null;
        var remaining = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken)));
        await DisposeAttemptsAsync(remaining);

        // Assert
        Assert.True(admissionWaited);
        Assert.NotNull(concurrent);
        Assert.Equal(4, remaining.Count(value => value is not null));
        Assert.Equal(0, cache.NativeConditionalMutations);
    }

    private static async Task<AuthService.LoginAttempt> BeginAsync(AuthService service)
    {
        var attempt = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        Assert.NotNull(attempt);
        return attempt;
    }

    private static AuthService CreateService(ICacheClient cache, TimeProvider timeProvider)
        => new(cache, timeProvider, NullLogger<AuthService>.Instance);

    private static FakeTimeProvider CreateTimeProvider()
        => new(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));

    private static Task DisposeAttemptsAsync(IEnumerable<AuthService.LoginAttempt?> attempts)
        => Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => attempt!.DisposeAsync().AsTask()));

    private sealed class InstrumentedCacheClient(TimeProvider timeProvider) : InMemoryCacheClient(options => options.TimeProvider(timeProvider)), ICacheClient
    {
        private int _reads;
        private int _removals;
        private int _nativeConditionalMutations;

        public IOException Failure { get; } = new("Synthetic outcome write failure.");
        public bool FailOutcomeWrites { get; set; }
        public bool SynchronousFailure { get; set; }
        public bool CommitBeforeFailure { get; set; }
        public bool FailUserOnly { get; set; }
        public Func<string, Task>? AfterRead { get; set; }
        public int ReadOperations => Volatile.Read(ref _reads);
        public int Removals => Volatile.Read(ref _removals);
        public int NativeConditionalMutations => Volatile.Read(ref _nativeConditionalMutations);

        async Task<CacheValue<T>> ICacheClient.GetAsync<T>(string cacheKey)
        {
            Interlocked.Increment(ref _reads);
            var result = await base.GetAsync<T>(cacheKey);
            if (AfterRead is not null && result.HasValue && result.Value is string value)
                await AfterRead(value);

            return result;
        }

        Task<bool> ICacheClient.RemoveAsync(string cacheKey)
        {
            Interlocked.Increment(ref _removals);
            return base.RemoveAsync(cacheKey);
        }

        Task<bool> ICacheClient.RemoveIfEqualAsync<T>(string cacheKey, T expected)
        {
            Interlocked.Increment(ref _nativeConditionalMutations);
            return base.RemoveIfEqualAsync(cacheKey, expected);
        }

        Task<bool> ICacheClient.ReplaceIfEqualAsync<T>(string cacheKey, T value, T expected, TimeSpan? expiresIn)
        {
            Interlocked.Increment(ref _nativeConditionalMutations);
            return base.ReplaceIfEqualAsync(cacheKey, value, expected, expiresIn);
        }

        Task<bool> ICacheClient.SetAsync<T>(string cacheKey, T value, TimeSpan? expiresIn)
        {
            bool shouldFail = FailOutcomeWrites && value is string text && text.StartsWith("failed:", StringComparison.Ordinal)
                && (!FailUserOnly || cacheKey.Contains("user:", StringComparison.Ordinal));
            if (!shouldFail)
                return base.SetAsync(cacheKey, value, expiresIn);

            if (SynchronousFailure)
                throw Failure;

            return WriteThenFailAsync(cacheKey, value, expiresIn);
        }

        private async Task<bool> WriteThenFailAsync<T>(string cacheKey, T value, TimeSpan? expiresIn)
        {
            if (CommitBeforeFailure)
                await base.SetAsync(cacheKey, value, expiresIn);

            throw Failure;
        }
    }
}
