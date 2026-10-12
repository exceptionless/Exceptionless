using Exceptionless.Core.Services;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public async Task ClearUserLoginAttemptsAsync_Recovery_PreservesIpFailuresAndChecksUnderway()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int i = 0; i < 4; i++)
            await FailAsync(service);

        await using var pending = await BeginAsync(service);

        // Act
        await service.ClearUserLoginAttemptsAsync(" User@exceptionless.test ");
        var remaining = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken)));
        await pending.DisposeAsync();
        await DisposeAttemptsAsync(remaining);
        for (int i = 0; i < 11; i++)
            await FailAsync(service, $"other{i}@exceptionless.test");

        var denied = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Equal(4, remaining.Count(attempt => attempt is not null));
        Assert.Null(denied);
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        var logger = Log.CreateLogger<AuthService>();

        // Act
        var cacheException = Record.Exception(() => new AuthService(null!, TimeProvider, logger));
        var timeException = Record.Exception(() => new AuthService(cache, null!, logger));
        var loggerException = Record.Exception(() => new AuthService(cache, TimeProvider, null!));

        // Assert
        Assert.Equal("cacheClient", Assert.IsType<ArgumentNullException>(cacheException).ParamName);
        Assert.Equal("timeProvider", Assert.IsType<ArgumentNullException>(timeException).ParamName);
        Assert.Equal("logger", Assert.IsType<ArgumentNullException>(loggerException).ParamName);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DisposeAsync_CleanupFailure_PreservesOriginalExceptionAndReleasesOtherCacheKeys(bool cancelled, bool asynchronousCleanupFailure)
    {
        // Arrange
        using var cache = new FaultingCacheClient(TimeProvider);
        var logger = new CapturingLogger();
        var service = new AuthService(cache, TimeProvider, logger);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        cancellation.Cancel();
        Exception failure = cancelled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("Synthetic request failure.");
        var cleanupFailure = new IOException("Synthetic cleanup failure.");
        var attemptedCacheKeys = new List<string>();
        cache.BeforeRemove = cacheKey =>
        {
            attemptedCacheKeys.Add(cacheKey);
            if (!cacheKey.Contains("user:", StringComparison.Ordinal))
                return null;

            return asynchronousCleanupFailure ? Task.FromException<bool>(cleanupFailure) : throw cleanupFailure;
        };

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var attempt = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);
            throw failure;
        });
        cache.BeforeRemove = null;
        var ipAttempts = await Task.WhenAll(Enumerable.Range(0, 15).Select(index => service.TryBeginLoginAsync($"other{index}@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        await DisposeAttemptsAsync(ipAttempts);

        // Assert
        Assert.Same(failure, exception);
        Assert.Equal(2, attemptedCacheKeys.Count);
        Assert.All(ipAttempts, Assert.NotNull);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(cleanupFailure, entry.Exception);
        Assert.Equal($"Error releasing login admission reservation: {cleanupFailure.Message}", entry.Message);
    }

    [Fact]
    public async Task DisposeAsync_CompletedFailure_RetainsCharge()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int i = 0; i < 5; i++)
            await FailAsync(service);

        // Act
        var denied = await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);

        // Assert
        Assert.Null(denied);
    }

    [Fact]
    public async Task DisposeAsync_InterruptedAttempt_ReleasesBothReservations()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        for (int i = 0; i < 30; i++)
            await (await BeginAsync(service)).DisposeAsync();

        await using var next = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.NotNull(next);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_NullAttempt_ThrowsArgumentNullException()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Record.ExceptionAsync(() => service.RecordLoginFailureAsync(null!));

        // Assert
        Assert.Equal("attempt", Assert.IsType<ArgumentNullException>(exception).ParamName);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ConcurrentFailures_PreservesNewFailuresAndOtherReservations()
    {
        // Arrange
        var service = GetService<AuthService>();
        await FailAsync(service);
        await using var success = await BeginAsync(service);
        var failures = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => BeginAsync(service)));
        await Task.WhenAll(failures.Select(service.RecordLoginFailureAsync));
        await DisposeAttemptsAsync(failures);

        // Act
        await service.RecordLoginSuccessAsync(success);
        await FailAsync(service);
        await FailAsync(service);
        await service.RecordLoginSuccessAsync(success);
        var denied = await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);

        // Assert
        Assert.Null(denied);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_NullAttempt_ThrowsArgumentNullException()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Record.ExceptionAsync(() => service.RecordLoginSuccessAsync(null!));

        // Assert
        Assert.Equal("attempt", Assert.IsType<ArgumentNullException>(exception).ParamName);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_SharedIpAddress_DoesNotRefundOtherUsersFailures()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int i = 0; i < 14; i++)
            await FailAsync(service, $"other{i}@exceptionless.test");

        await using var success = await BeginAsync(service);

        // Act
        await service.RecordLoginSuccessAsync(success);
        await FailAsync(service, "last@exceptionless.test");
        var denied = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Null(denied);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ValidRequests_DoNotConsumeFailureQuota()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        for (int batch = 0; batch < 20; batch++)
        {
            var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => BeginAsync(service)));
            await Task.WhenAll(attempts.Select(service.RecordLoginSuccessAsync));
            await DisposeAttemptsAsync(attempts);
        }

        await using var next = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.NotNull(next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryBeginLoginAsync_CancelledAfterReservation_ReleasesBothCacheKeys(bool includeIpAddress)
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cache = new FaultingCacheClient(TimeProvider);
        string? ipAddress = includeIpAddress ? "192.0.2.1" : null;
        cache.AfterAdd = cacheKey =>
        {
            if (cacheKey.Contains(includeIpAddress ? "ip:" : "user:", StringComparison.Ordinal))
                cancellation.Cancel();
        };

        var service = new AuthService(cache, TimeProvider, Log.CreateLogger<AuthService>());

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@exceptionless.test", ipAddress, cancellation.Token);
        });
        cache.AfterAdd = null;
        var allowed = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", ipAddress, TestCancellationToken)));
        var ipAllowed = await Task.WhenAll(Enumerable.Range(0, 10).Select(index => service.TryBeginLoginAsync($"other{index}@exceptionless.test", ipAddress, TestCancellationToken)));
        await DisposeAttemptsAsync(allowed.Concat(ipAllowed));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.All(allowed, Assert.NotNull);
        Assert.All(ipAllowed, Assert.NotNull);
    }

    [Fact]
    public async Task TryBeginLoginAsync_CancelledRequest_Throws()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        await cancellation.CancelAsync();
        var service = GetService<AuthService>();

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@example.test", null, cancellation.Token);
        });

        // Assert
        Assert.IsType<OperationCanceledException>(exception);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TryBeginLoginAsync_CleanupFailure_PreservesOriginalExceptionAndLogsCleanupFailure(bool cancelled, bool asynchronousCleanupFailure)
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cache = new FaultingCacheClient(TimeProvider);
        var failure = new InvalidOperationException("Synthetic acquisition failure.");
        cache.BeforeAdd = cacheKey =>
        {
            if (!cancelled && cacheKey.Contains("ip:", StringComparison.Ordinal))
                throw failure;
        };

        cache.AfterAdd = cacheKey =>
        {
            if (cancelled && cacheKey.Contains("ip:", StringComparison.Ordinal))
                cancellation.Cancel();
        };

        var cleanupFailure = new IOException("Synthetic cleanup failure.");
        cache.BeforeRemove = _ => asynchronousCleanupFailure ? Task.FromException<bool>(cleanupFailure) : throw cleanupFailure;
        var logger = new CapturingLogger();
        var service = new AuthService(cache, TimeProvider, logger);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", cancellation.Token);
        });
        cache.BeforeAdd = null;
        cache.AfterAdd = null;
        cache.BeforeRemove = null;
        var beforeExpiration = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken)));
        await DisposeAttemptsAsync(beforeExpiration);
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var afterExpiration = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken)));
        await DisposeAttemptsAsync(afterExpiration);

        // Assert
        if (cancelled)
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(exception).CancellationToken);
        else
            Assert.Same(failure, exception);

        Assert.Equal(cancelled ? 2 : 1, logger.Entries.Count);
        Assert.All(logger.Entries, entry =>
        {
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Same(cleanupFailure, entry.Exception);
            Assert.Equal($"Error releasing login admission reservation: {cleanupFailure.Message}", entry.Message);
        });

        Assert.Equal(4, beforeExpiration.Count(attempt => attempt is not null));
        Assert.All(afterExpiration, Assert.NotNull);
    }

    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentInstances_BoundsChecksBeforeFailuresComplete()
    {
        // Arrange
        var first = GetService<AuthService>();
        var second = new AuthService(GetService<ICacheClient>(), TimeProvider, Log.CreateLogger<AuthService>());

        // Act
        var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(index =>
            (index % 2 == 0 ? first : second).TryBeginLoginAsync(" User@exceptionless.test ", null, TestCancellationToken)));
        await Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => first.RecordLoginFailureAsync(attempt!)));
        await DisposeAttemptsAsync(attempts);
        var denied = await second.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);

        // Assert
        Assert.Equal(5, attempts.Count(attempt => attempt is not null));
        Assert.Null(denied);
    }

    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentUsers_BoundsChecksAtSharedIpAddress()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(index =>
            service.TryBeginLoginAsync($"user{index}@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        await Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        await DisposeAttemptsAsync(attempts);
        var denied = await service.TryBeginLoginAsync("other@exceptionless.test", "192.0.2.1", TestCancellationToken);
        var allowed = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            service.TryBeginLoginAsync("other@exceptionless.test", "192.0.2.2", TestCancellationToken)));
        await DisposeAttemptsAsync(allowed);

        // Assert
        Assert.Equal(15, attempts.Count(attempt => attempt is not null));
        Assert.Null(denied);
        Assert.All(allowed, Assert.NotNull);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryBeginLoginAsync_InvalidEmail_Throws(string? email)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var beginException = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync(email!, null, TestCancellationToken);
        });
        var clearException = await Record.ExceptionAsync(() => service.ClearUserLoginAttemptsAsync(email!));

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(beginException);
        Assert.IsAssignableFrom<ArgumentException>(clearException);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryBeginLoginAsync_InvalidIpAddress_Throws(string address)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@example.test", address, TestCancellationToken);
        });

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(exception);
    }

    [Fact]
    public async Task TryBeginLoginAsync_IpCacheFailure_ReleasesUserCacheKey()
    {
        // Arrange
        using var cache = new FaultingCacheClient(TimeProvider);
        var failure = new InvalidOperationException("Synthetic cache failure.");
        cache.BeforeAdd = cacheKey =>
        {
            if (cacheKey.Contains("ip:", StringComparison.Ordinal))
                throw failure;
        };

        var service = new AuthService(cache, TimeProvider, Log.CreateLogger<AuthService>());

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);
        });
        cache.BeforeAdd = null;
        var allowed = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        await DisposeAttemptsAsync(allowed);

        // Assert
        Assert.Same(failure, exception);
        Assert.All(allowed, Assert.NotNull);
    }

    [Fact]
    public async Task TryBeginLoginAsync_QuarterHour_ExpiresFailuresAndAbandonedReservations()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 14, 0, TimeSpan.Zero));
        var service = GetService<AuthService>();
        await using var old = await BeginAsync(service);
        for (int i = 0; i < 4; i++)
            await FailAsync(service);

        // Act
        var beforeBoundary = await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var current = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        await Task.WhenAll(current.Where(attempt => attempt is not null).Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        await DisposeAttemptsAsync(current);
        await service.RecordLoginSuccessAsync(old);
        await service.RecordLoginFailureAsync(old);
        await old.DisposeAsync();
        var afterOldCompletion = await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await using var next = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Null(beforeBoundary);
        Assert.All(current, Assert.NotNull);
        Assert.Null(afterOldCompletion);
        Assert.NotNull(next);
    }

    private async Task<AuthService.LoginAttempt> BeginAsync(AuthService service, string email = "user@exceptionless.test")
    {
        var attempt = await service.TryBeginLoginAsync(email, "192.0.2.1", TestCancellationToken);
        Assert.NotNull(attempt);

        return attempt;
    }

    private static Task DisposeAttemptsAsync(IEnumerable<AuthService.LoginAttempt?> attempts)
        => Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => attempt!.DisposeAsync().AsTask()));

    private async Task FailAsync(AuthService service, string email = "user@exceptionless.test")
    {
        await using var attempt = await BeginAsync(service, email);
        await service.RecordLoginFailureAsync(attempt);
    }

    private sealed class CapturingLogger : ILogger<AuthService>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class FaultingCacheClient(TimeProvider timeProvider) : InMemoryCacheClient(options => options.TimeProvider(timeProvider)), ICacheClient
    {
        public Action<string>? BeforeAdd { get; set; }
        public Action<string>? AfterAdd { get; set; }
        public Func<string, Task<bool>?>? BeforeRemove { get; set; }

        async Task<bool> ICacheClient.AddAsync<T>(string cacheKey, T value, TimeSpan? expiresIn)
        {
            BeforeAdd?.Invoke(cacheKey);

            bool added = await base.AddAsync(cacheKey, value, expiresIn);
            if (added)
                AfterAdd?.Invoke(cacheKey);

            return added;
        }

        Task<bool> ICacheClient.RemoveIfEqualAsync<T>(string cacheKey, T expected)
            => BeforeRemove?.Invoke(cacheKey) ?? base.RemoveIfEqualAsync(cacheKey, expected);
    }
}
