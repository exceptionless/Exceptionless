using Exceptionless.Core.Services;
using Foundatio.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceAdmissionTests
{
    private const string EmailAddress = "user@exceptionless.test";
    private const string IpAddress = "192.0.2.1";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WaitForLoginAsync_CancelledDuringSaturationRead_PreservesCancellationAndFailures()
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new AdmissionCacheClient(clock);
        var service = CreateService(cache, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);
        await Task.WhenAll(occupied.Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        int reads = 0;
        cache.AfterRead = () =>
        {
            if (++reads == 2)
                cancellation.Cancel();
        };

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token);
        });
        cache.AfterRead = null;
        await using var denied = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await DisposeAsync(occupied);

        // Assert
        Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(exception).CancellationToken);
        Assert.Null(denied);
        Assert.Equal(10, cache.Keys.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitForLoginAsync_CompletedFailures_DeniesWithoutWaiting(bool sharedIp)
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        var service = CreateService(cache, clock);
        int limit = sharedIp ? 15 : 5;
        for (int index = 0; index < limit; index++)
        {
            await using var attempt = await service.TryBeginLoginAsync(sharedIp ? $"user{index}@exceptionless.test" : EmailAddress, IpAddress, TestCancellationToken);
            Assert.NotNull(attempt);
            await service.RecordLoginFailureAsync(attempt);
        }

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);

        // Assert
        Assert.Null(result);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), clock.GetUtcNow());
    }

    [Fact]
    public async Task WaitForLoginAsync_PendingChecksComplete_AdmitsWithoutIncreasingCapacity()
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        var service = CreateService(cache, clock);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        bool waited = !pending.IsCompleted;
        await service.RecordLoginSuccessAsync(occupied[0]!);
        clock.Advance(TimeSpan.FromMilliseconds(50));
        await using var admitted = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        await using var excess = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await DisposeAsync(occupied);

        // Assert
        Assert.True(waited);
        Assert.NotNull(admitted);
        Assert.Null(excess);
    }

    [Fact]
    public async Task WaitForLoginAsync_PendingChecksFail_DeniesWithoutAdditionalVerification()
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        var service = CreateService(cache, clock);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await Task.WhenAll(occupied.Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        clock.Advance(TimeSpan.FromMilliseconds(50));
        var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        await DisposeAsync(occupied);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitForLoginAsync_PendingChecksNeverComplete_StopsAtDeadlineOrCancellation(bool cancel)
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        var service = CreateService(cache, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token);
        if (cancel)
            await cancellation.CancelAsync();
        else
            clock.Advance(TimeSpan.FromSeconds(2));

        AuthService.LoginAttempt? result = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        });
        await using var excess = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await DisposeAsync(occupied);
        var remaining = await ReserveAsync(service, 5);
        await DisposeAsync(remaining);

        // Assert
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(exception).CancellationToken);
        else
            Assert.Null(exception);

        Assert.Null(result);
        Assert.Null(excess);
        Assert.All(remaining, Assert.NotNull);
    }

    [Fact]
    public async Task WaitForLoginAsync_RejectedWrites_PacesRetriesUntilDeadline()
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new AdmissionCacheClient(clock);
        var service = CreateService(cache, clock);
        var releaseRetry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        cache.BeforeAdd = async () =>
        {
            // Stop an unpaced implementation at its first retry instead of allowing a busy loop.
            if (++writes > 5)
                await releaseRetry.Task.WaitAsync(TestTimeout, TestCancellationToken);

            return false;
        };

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        int writesBeforeAdvance = writes;
        clock.Advance(TimeSpan.FromSeconds(2));
        releaseRetry.TrySetResult();
        var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);

        // Assert
        Assert.Equal(5, writesBeforeAdvance);
        Assert.Null(result);
        Assert.Empty(cache.Keys);
    }

    [Fact]
    public async Task WaitForLoginAsync_RetryCompletesAfterDeadline_ReleasesAdmission()
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new AdmissionCacheClient(clock);
        var service = CreateService(cache, clock);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await occupied[0]!.DisposeAsync();
        cache.AfterAdd = () =>
        {
            cache.AfterAdd = null;
            clock.Advance(TimeSpan.FromSeconds(2));
        };
        clock.Advance(TimeSpan.FromMilliseconds(50));
        await using var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        int reservedEntries = cache.Keys.Count;
        await DisposeAsync(occupied);

        // Assert
        Assert.Null(result);
        Assert.Equal(8, reservedEntries);
    }

    [Fact]
    public async Task WaitForLoginAsync_SharedIpIsBusy_ReleasesUserCapacityBeforeWaiting()
    {
        // Arrange
        var clock = CreateTimeProvider();
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        var service = CreateService(cache, clock);
        var occupied = await Task.WhenAll(Enumerable.Range(0, 15).Select(index =>
            service.TryBeginLoginAsync($"other{index}@exceptionless.test", IpAddress, TestCancellationToken)));
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        var otherIp = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            service.TryBeginLoginAsync(EmailAddress, "192.0.2.2", TestCancellationToken)));
        await DisposeAsync(otherIp);
        await occupied[0]!.DisposeAsync();
        clock.Advance(TimeSpan.FromMilliseconds(50));
        await using var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        await DisposeAsync(occupied);

        // Assert
        Assert.All(otherIp, Assert.NotNull);
        Assert.NotNull(result);
    }

    private static AuthService CreateService(ICacheClient cache, TimeProvider clock)
        => new(cache, clock, NullLogger<AuthService>.Instance);

    private static FakeTimeProvider CreateTimeProvider()
        => new(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));

    private static Task DisposeAsync(IEnumerable<AuthService.LoginAttempt?> attempts)
        => Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => attempt!.DisposeAsync().AsTask()));

    private static Task<AuthService.LoginAttempt?[]> ReserveAsync(AuthService service, int count)
        => Task.WhenAll(Enumerable.Range(0, count).Select(_ => service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken)));

    private sealed class AdmissionCacheClient(TimeProvider clock) : InMemoryCacheClient(options => options.TimeProvider(clock)), ICacheClient
    {
        public Func<Task<bool>>? BeforeAdd { get; set; }
        public Action? AfterAdd { get; set; }
        public Action? AfterRead { get; set; }

        async Task<bool> ICacheClient.AddAsync<T>(string key, T value, TimeSpan? expiresIn)
        {
            if (BeforeAdd is not null)
                return await BeforeAdd();

            bool added = await base.AddAsync(key, value, expiresIn);
            if (added)
                AfterAdd?.Invoke();

            return added;
        }

        async Task<IDictionary<string, CacheValue<T>>> ICacheClient.GetAllAsync<T>(IEnumerable<string> keys)
        {
            var entries = await base.GetAllAsync<T>(keys);
            AfterRead?.Invoke();
            return entries;
        }
    }
}
