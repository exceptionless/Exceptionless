using Exceptionless.Core.Services;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LoginState = Exceptionless.Core.Services.AuthService.LoginState;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceTests : TestWithServices
{
    private const string EmailAddress = "user@exceptionless.test";
    private const string IpAddress = "192.0.2.1";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    public AuthServiceTests(ITestOutputHelper output) : base(output)
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ClearUserLoginAttemptsAsync_ConcurrentFailure_PreservesNewFailureAndPendingReservation()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        await FailAsync(service);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        bool? staleWriteCommitted = null;
        cacheMock.Setup(client => client.ReplaceIfEqualAsync(
                It.Is<string>(key => key.Contains("user:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async (string key, LoginState value, LoginState expected, TimeSpan? expiresIn) =>
            {
                if (Interlocked.Increment(ref writes) != 1)
                    return await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);

                observed.TrySetResult();
                await continueWrite.Task.WaitAsync(TestTimeout, TestCancellationToken);
                staleWriteCommitted = await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);
                return staleWriteCommitted.Value;
            });

        // Act
        var recovery = service.ClearUserLoginAttemptsAsync(EmailAddress);
        AuthService.LoginAttempt? pending = null;
        try
        {
            await observed.Task.WaitAsync(TestTimeout, TestCancellationToken);
            await FailAsync(service);
            pending = await BeginAsync(service);
        }
        finally
        {
            continueWrite.TrySetResult();
        }

        await recovery.WaitAsync(TestTimeout, TestCancellationToken);
        var remaining = await ReserveAsync(service, 5);
        await DisposeAttemptsAsync(remaining.Append(pending));

        // Assert
        Assert.False(staleWriteCommitted);
        Assert.Equal(3, remaining.Count(attempt => attempt is not null));
    }

    [Fact]
    public async Task ClearUserLoginAttemptsAsync_Recovery_PreservesIpFailuresAndPendingReservations()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int i = 0; i < 4; i++)
            await FailAsync(service);

        await using var pending = await BeginAsync(service);

        // Act
        await service.ClearUserLoginAttemptsAsync(" User@exceptionless.test ");
        var remaining = await ReserveAsync(service, 5, ipAddress: null);
        await pending.DisposeAsync();
        await DisposeAttemptsAsync(remaining);
        for (int i = 0; i < 11; i++)
            await FailAsync(service, $"other{i}@exceptionless.test");

        await using var denied = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);

        // Assert
        Assert.Equal(4, remaining.Count(attempt => attempt is not null));
        Assert.Null(denied);
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = GetService<ICacheClient>();

        // Act
        Action withoutCache = () => _ = new AuthService(null!, TimeProvider, Log);
        Action withoutTimeProvider = () => _ = new AuthService(cache, null!, Log);
        Action withoutLoggerFactory = () => _ = new AuthService(cache, TimeProvider, null!);

        // Assert
        Assert.Equal("cacheClient", Assert.Throws<ArgumentNullException>(withoutCache).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(withoutTimeProvider).ParamName);
        Assert.Equal("loggerFactory", Assert.Throws<ArgumentNullException>(withoutLoggerFactory).ParamName);
    }

    [Fact]
    public async Task DisposeAsync_CleanupFailure_PreservesRequestFailureAndReleasesOtherBudget()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        var requestFailure = new InvalidOperationException("Synthetic request failure.");
        var cleanupFailure = new IOException("Synthetic cleanup failure.");
        bool rejectCleanup = true;
        cacheMock.Setup(client => client.ReplaceIfEqualAsync(
                It.Is<string>(key => key.Contains("user:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns((string key, LoginState value, LoginState expected, TimeSpan? expiresIn) =>
                rejectCleanup ? Task.FromException<bool>(cleanupFailure) : cache.ReplaceIfEqualAsync(key, value, expected, expiresIn));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var attempt = await BeginAsync(service);
            throw requestFailure;
        });
        rejectCleanup = false;
        var ipAttempts = await Task.WhenAll(Enumerable.Range(0, 15).Select(index =>
            service.TryBeginLoginAsync($"other{index}@exceptionless.test", IpAddress, TestCancellationToken)));
        await DisposeAttemptsAsync(ipAttempts);

        // Assert
        Assert.Same(requestFailure, exception);
        Assert.All(ipAttempts, Assert.NotNull);
        var entry = Assert.Single(Log.LogEntries.Where(entry => entry.LogLevel == LogLevel.Error));
        Assert.Same(cleanupFailure, entry.Exception);
        Assert.Equal($"Error releasing login admission reservation: {cleanupFailure.Message}", entry.Message);
    }

    [Fact]
    public async Task DisposeAsync_InterruptedAttempts_ReleaseBothBudgets()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        for (int i = 0; i < 16; i++)
        {
            var attempt = await BeginAsync(service);
            await Task.WhenAll(attempt.DisposeAsync().AsTask(), attempt.DisposeAsync().AsTask());
        }

        await using var next = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);

        // Assert
        Assert.NotNull(next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecordLoginFailureAsync_FailedOutcomeWrite_RetainsBothBudgets(bool commitBeforeFailure)
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        var attempt = await BeginAsync(service);
        var failure = new IOException("Synthetic outcome write failure.");
        bool rejectWrites = true;
        cacheMock.Setup(client => client.ReplaceIfEqualAsync(
                It.Is<string>(key => key.Contains("user:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async (string key, LoginState value, LoginState expected, TimeSpan? expiresIn) =>
            {
                if (!rejectWrites)
                    return await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);

                if (commitBeforeFailure)
                    await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);

                throw failure;
            });

        // Act
        var exception = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using (attempt)
                await service.RecordLoginFailureAsync(attempt);
        });
        await attempt.DisposeAsync();
        rejectWrites = false;
        var userAttempts = await ReserveAsync(service, 5, ipAddress: null);
        var ipAttempts = await Task.WhenAll(Enumerable.Range(0, 15).Select(index =>
            service.TryBeginLoginAsync($"other{index}@exceptionless.test", IpAddress, TestCancellationToken)));
        await DisposeAttemptsAsync(userAttempts.Concat(ipAttempts));
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var afterExpiration = await ReserveAsync(service, 5);
        await DisposeAttemptsAsync(afterExpiration);

        // Assert
        Assert.Same(failure, exception);
        Assert.Equal(4, userAttempts.Count(value => value is not null));
        Assert.Equal(14, ipAttempts.Count(value => value is not null));
        Assert.All(afterExpiration, Assert.NotNull);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_NullAttempt_ThrowsArgumentNullException()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        Func<Task> recordFailure = () => service.RecordLoginFailureAsync(null!);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(recordFailure);
        Assert.Equal("attempt", exception.ParamName);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_ReplacedState_PreservesNewReservations()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var service = CreateService(cache);
        await using var old = await BeginAsync(service);
        await cache.RemoveAllAsync();
        var current = await ReserveAsync(service, 5);
        Assert.All(current, Assert.NotNull);

        // Act
        await service.RecordLoginFailureAsync(old);
        await old.DisposeAsync();
        await using var denied = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await DisposeAttemptsAsync(current);
        var remaining = await ReserveAsync(service, 5);
        await DisposeAttemptsAsync(remaining);

        // Assert
        Assert.Null(denied);
        Assert.All(remaining, Assert.NotNull);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_AlreadyFailedAttempt_PreservesEarlierFailures()
    {
        // Arrange
        var service = GetService<AuthService>();
        await FailAsync(service);
        await using var failed = await BeginAsync(service);
        await service.RecordLoginFailureAsync(failed);

        // Act
        await service.RecordLoginSuccessAsync(failed);
        var remaining = await ReserveAsync(service, 5, ipAddress: null);
        await DisposeAttemptsAsync(remaining);

        // Assert
        Assert.Equal(3, remaining.Count(attempt => attempt is not null));
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ConcurrentFailures_PreservesNewFailuresAndReservations()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var first = CreateService(cacheMock.Object);
        var second = CreateService(cacheMock.Object);
        await FailAsync(first);
        await using var success = await BeginAsync(first);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        bool? staleWriteCommitted = null;
        cacheMock.Setup(client => client.ReplaceIfEqualAsync(
                It.Is<string>(key => key.Contains("user:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async (string key, LoginState value, LoginState expected, TimeSpan? expiresIn) =>
            {
                if (Interlocked.Increment(ref writes) != 1)
                    return await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);

                observed.TrySetResult();
                await continueWrite.Task.WaitAsync(TestTimeout, TestCancellationToken);
                staleWriteCommitted = await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);
                return staleWriteCommitted.Value;
            });

        // Act
        var completion = first.RecordLoginSuccessAsync(success);
        AuthService.LoginAttempt? pending = null;
        try
        {
            await observed.Task.WaitAsync(TestTimeout, TestCancellationToken);
            await FailAsync(second);
            pending = await BeginAsync(second);
        }
        finally
        {
            continueWrite.TrySetResult();
        }

        await completion.WaitAsync(TestTimeout, TestCancellationToken);
        var remaining = await ReserveAsync(second, 5, ipAddress: null);
        await first.RecordLoginSuccessAsync(success);
        await first.RecordLoginFailureAsync(success);
        await using var denied = await second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
        await DisposeAttemptsAsync(remaining.Append(pending));

        // Assert
        Assert.False(staleWriteCommitted);
        Assert.Equal(3, remaining.Count(attempt => attempt is not null));
        Assert.Null(denied);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_NullAttempt_ThrowsArgumentNullException()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        Func<Task> recordSuccess = () => service.RecordLoginSuccessAsync(null!);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(recordSuccess);
        Assert.Equal("attempt", exception.ParamName);
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
        await using var denied = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);

        // Assert
        Assert.Null(denied);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ValidRequests_DoNotConsumeFailureQuota()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        for (int batch = 0; batch < 2; batch++)
        {
            var attempts = await ReserveAsync(service, 5);
            Assert.All(attempts, Assert.NotNull);
            await Task.WhenAll(attempts.Select(attempt => service.RecordLoginSuccessAsync(attempt!)));
            await DisposeAttemptsAsync(attempts);
        }

        await using var next = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);

        // Assert
        Assert.NotNull(next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryBeginLoginAsync_CancelledAfterReservation_ReleasesBothBudgets(bool includeIpAddress)
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        string? ipAddress = includeIpAddress ? IpAddress : null;
        bool cancelAfterAdd = true;
        cacheMock.Setup(client => client.AddAsync(
                It.Is<string>(key => key.Contains(includeIpAddress ? "ip:" : "user:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async (string key, LoginState value, TimeSpan? expiresIn) =>
            {
                bool added = await cache.AddAsync(key, value, expiresIn);
                if (added && cancelAfterAdd)
                    await cancellation.CancelAsync();

                return added;
            });

        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.TryBeginLoginAsync(EmailAddress, ipAddress, cancellation.Token));
        cancelAfterAdd = false;
        var allowed = await ReserveAsync(service, 5, ipAddress: ipAddress);
        AuthService.LoginAttempt?[] ipAllowed = includeIpAddress
            ? await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
                service.TryBeginLoginAsync($"other{index}@exceptionless.test", IpAddress, TestCancellationToken)))
            : [];
        await DisposeAttemptsAsync(allowed.Concat(ipAllowed));

        // Assert
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.All(allowed, Assert.NotNull);
        Assert.All(ipAllowed, Assert.NotNull);
    }

    [Fact]
    public async Task TryBeginLoginAsync_CancelledRequest_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        await cancellation.CancelAsync();
        var service = GetService<AuthService>();

        // Act
        Func<Task> begin = () => service.TryBeginLoginAsync(EmailAddress, null, cancellation.Token);

        // Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(begin);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryBeginLoginAsync_CleanupFailure_PreservesOriginalExceptionAndLogsCleanupFailure(bool cancelled)
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        var acquisitionFailure = new InvalidOperationException("Synthetic acquisition failure.");
        var cleanupFailure = new IOException("Synthetic cleanup failure.");
        bool injectFailures = true;
        cacheMock.Setup(client => client.AddAsync(
                It.Is<string>(key => key.Contains("ip:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async (string key, LoginState value, TimeSpan? expiresIn) =>
            {
                if (injectFailures && !cancelled)
                    throw acquisitionFailure;

                bool added = await cache.AddAsync(key, value, expiresIn);
                if (injectFailures && added)
                    await cancellation.CancelAsync();

                return added;
            });
        cacheMock.Setup(client => client.ReplaceIfEqualAsync(
                It.IsAny<string>(), It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns((string key, LoginState value, LoginState expected, TimeSpan? expiresIn) =>
                injectFailures ? Task.FromException<bool>(cleanupFailure) : cache.ReplaceIfEqualAsync(key, value, expected, expiresIn));

        // Act
        var acquisition = service.TryBeginLoginAsync(EmailAddress, IpAddress, cancellation.Token);
        Exception exception = cancelled
            ? await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acquisition)
            : await Assert.ThrowsAsync<InvalidOperationException>(() => acquisition);
        injectFailures = false;
        var beforeExpiration = await ReserveAsync(service, 5, ipAddress: null);
        await DisposeAttemptsAsync(beforeExpiration);
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var afterExpiration = await ReserveAsync(service, 5);
        await DisposeAttemptsAsync(afterExpiration);

        // Assert
        if (cancelled)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(exception).CancellationToken);
        else
            Assert.Same(acquisitionFailure, exception);

        var entries = Log.LogEntries.Where(entry => entry.LogLevel == LogLevel.Error).ToArray();
        Assert.Equal(cancelled ? 2 : 1, entries.Length);
        Assert.All(entries, entry =>
        {
            Assert.Same(cleanupFailure, entry.Exception);
            Assert.Equal($"Error releasing login admission reservation: {cleanupFailure.Message}", entry.Message);
        });
        Assert.Equal(4, beforeExpiration.Count(attempt => attempt is not null));
        Assert.All(afterExpiration, Assert.NotNull);
    }

    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentStateChange_RetriesWithinSharedAccountLimit()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var first = CreateService(cacheMock.Object);
        var second = CreateService(cacheMock.Object);
        var occupied = await ReserveAsync(first, 4, ipAddress: null);
        Assert.All(occupied, Assert.NotNull);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        cacheMock.Setup(client => client.GetAsync<LoginState>(It.IsAny<string>()))
            .Returns(async (string key) =>
            {
                var state = await cache.GetAsync<LoginState>(key);
                if (Interlocked.Increment(ref reads) == 1)
                {
                    observed.TrySetResult();
                    await continueRead.Task.WaitAsync(TestTimeout, TestCancellationToken);
                }

                return state;
            });

        // Act
        var staleAdmission = first.TryBeginLoginAsync(" User@exceptionless.test ", null, TestCancellationToken);
        AuthService.LoginAttempt? admitted = null;
        try
        {
            await observed.Task.WaitAsync(TestTimeout, TestCancellationToken);
            admitted = await second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
        }
        finally
        {
            continueRead.TrySetResult();
        }

        await using var denied = await staleAdmission.WaitAsync(TestTimeout, TestCancellationToken);
        await Task.WhenAll(occupied.Append(admitted).Where(attempt => attempt is not null)
            .Select(attempt => first.RecordLoginFailureAsync(attempt!)));
        await DisposeAttemptsAsync(occupied.Append(admitted));
        await using var afterFailures = await second.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);

        // Assert
        Assert.NotNull(admitted);
        Assert.Null(denied);
        Assert.Null(afterFailures);
    }

    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentUsers_BoundsChecksAtSharedIpAddress()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var attempts = await Task.WhenAll(Enumerable.Range(0, 16).Select(index =>
            service.TryBeginLoginAsync($"user{index}@exceptionless.test", IpAddress, TestCancellationToken)));
        await Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        await DisposeAttemptsAsync(attempts);
        await using var denied = await service.TryBeginLoginAsync("other@exceptionless.test", IpAddress, TestCancellationToken);
        var allowed = await ReserveAsync(service, 5, emailAddress: "other@exceptionless.test", ipAddress: "192.0.2.2");
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
    public async Task TryBeginLoginAsync_InvalidEmail_ThrowsArgumentException(string? email)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        Func<Task> begin = () => service.TryBeginLoginAsync(email!, null, TestCancellationToken);
        Func<Task> clear = () => service.ClearUserLoginAttemptsAsync(email!);

        // Assert
        var beginException = await Assert.ThrowsAnyAsync<ArgumentException>(begin);
        var clearException = await Assert.ThrowsAnyAsync<ArgumentException>(clear);
        Assert.Equal("emailAddress", beginException.ParamName);
        Assert.Equal("emailAddress", clearException.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryBeginLoginAsync_InvalidIpAddress_ThrowsArgumentException(string ipAddress)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        Func<Task> begin = () => service.TryBeginLoginAsync(EmailAddress, ipAddress, TestCancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(begin);
        Assert.Equal("ipAddress", exception.ParamName);
    }

    [Fact]
    public async Task TryBeginLoginAsync_IpCacheFailure_ReleasesAccountReservation()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        var failure = new IOException("Synthetic IP cache failure.");
        bool rejectIp = true;
        cacheMock.Setup(client => client.AddAsync(
                It.Is<string>(key => key.Contains("ip:", StringComparison.Ordinal)),
                It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns((string key, LoginState value, TimeSpan? expiresIn) =>
                rejectIp ? Task.FromException<bool>(failure) : cache.AddAsync(key, value, expiresIn));

        // Act
        var exception = await Assert.ThrowsAsync<IOException>(() => service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken));
        rejectIp = false;
        var allowed = await ReserveAsync(service, 5);
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
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        await using var old = await BeginAsync(service);
        await using var closingFailure = await BeginAsync(service);
        await using var closingSuccess = await BeginAsync(service);
        for (int i = 0; i < 2; i++)
            await FailAsync(service);

        // Act
        await using var beforeBoundary = await service.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
        TimeProvider.Advance(TimeSpan.FromMinutes(1) - CacheClientExtensions.MinimumExpiration);
        await service.RecordLoginFailureAsync(closingFailure);
        await service.RecordLoginSuccessAsync(closingSuccess);
        await service.ClearUserLoginAttemptsAsync(EmailAddress);
        await using var nearBoundary = await service.TryBeginLoginAsync("other@exceptionless.test", IpAddress, TestCancellationToken);
        TimeProvider.Advance(CacheClientExtensions.MinimumExpiration);
        var current = await ReserveAsync(service, 5);
        await Task.WhenAll(current.Where(attempt => attempt is not null).Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        await DisposeAttemptsAsync(current);
        await service.RecordLoginSuccessAsync(old);
        await service.RecordLoginFailureAsync(old);
        await old.DisposeAsync();
        await using var afterOldCompletion = await service.TryBeginLoginAsync(EmailAddress, null, TestCancellationToken);
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await using var next = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);

        // Assert
        Assert.Null(beforeBoundary);
        Assert.Null(nearBoundary);
        Assert.All(current, Assert.NotNull);
        Assert.Null(afterOldCompletion);
        Assert.NotNull(next);
        // Foundatio treats tiny expirations as removal; do not issue mutations at that boundary.
        cacheMock.Verify(client => client.AddAsync(It.IsAny<string>(), It.IsAny<LoginState>(),
            It.Is<TimeSpan?>(expiresIn => expiresIn.HasValue && expiresIn.Value <= CacheClientExtensions.MinimumExpiration)), Times.Never);
        cacheMock.Verify(client => client.ReplaceIfEqualAsync(It.IsAny<string>(), It.IsAny<LoginState>(), It.IsAny<LoginState>(),
            It.Is<TimeSpan?>(expiresIn => expiresIn.HasValue && expiresIn.Value <= CacheClientExtensions.MinimumExpiration)), Times.Never);
    }

    [Fact]
    public async Task WaitForLoginAsync_CancelledDuringSaturationRead_PreservesCancellationAndFailures()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        for (int i = 0; i < 5; i++)
            await FailAsync(service);

        int reads = 0;
        cacheMock.Setup(client => client.GetAsync<LoginState>(It.IsAny<string>()))
            .Returns(async (string key) =>
            {
                var state = await cache.GetAsync<LoginState>(key);
                if (Interlocked.Increment(ref reads) == 2)
                    await cancellation.CancelAsync();

                return state;
            });

        // Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token));
        await using var denied = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);

        // Assert
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Null(denied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitForLoginAsync_CompletedFailures_DeniesWithoutWaiting(bool sharedIp)
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int index = 0; index < (sharedIp ? 15 : 5); index++)
            await FailAsync(service, sharedIp ? $"user{index}@exceptionless.test" : EmailAddress);

        // Act
        var result = await service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken).WaitAsync(TestTimeout, TestCancellationToken);

        // Assert
        Assert.Null(result);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task WaitForLoginAsync_PendingChecksComplete_AdmitsWithoutIncreasingCapacity()
    {
        // Arrange
        var service = GetService<AuthService>();
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        bool waited = !pending.IsCompleted;
        await service.RecordLoginSuccessAsync(occupied[0]!);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(50));
        await using var admitted = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        await using var excess = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await DisposeAttemptsAsync(occupied);

        // Assert
        Assert.True(waited);
        Assert.NotNull(admitted);
        Assert.Null(excess);
    }

    [Fact]
    public async Task WaitForLoginAsync_PendingChecksFail_DeniesFurtherAdmission()
    {
        // Arrange
        var service = GetService<AuthService>();
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await Task.WhenAll(occupied.Select(attempt => service.RecordLoginFailureAsync(attempt!)));
        TimeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        await DisposeAttemptsAsync(occupied);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task WaitForLoginAsync_RejectedWrites_PacesRetriesUntilDeadline()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        var continueWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        cacheMock.Setup(client => client.AddAsync(It.IsAny<string>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async () =>
            {
                // Stop an unpaced implementation at its next retry rather than allowing a busy loop.
                if (Interlocked.Increment(ref writes) > 8)
                    await continueWrite.Task.WaitAsync(TestTimeout, TestCancellationToken);

                return false;
            });

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        int writesBeforeAdvance = Volatile.Read(ref writes);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        continueWrite.TrySetResult();
        var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);

        // Assert
        Assert.InRange(writesBeforeAdvance, 1, 8);
        Assert.Null(result);
    }

    [Fact]
    public async Task WaitForLoginAsync_RetryCompletesAfterDeadline_ReleasesAdmission()
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        await occupied[0]!.DisposeAsync();
        int writes = 0;
        cacheMock.Setup(client => client.ReplaceIfEqualAsync(
                It.IsAny<string>(), It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns(async (string key, LoginState value, LoginState expected, TimeSpan? expiresIn) =>
            {
                bool written = await cache.ReplaceIfEqualAsync(key, value, expected, expiresIn);
                if (written && Interlocked.Increment(ref writes) == 1)
                    TimeProvider.Advance(TimeSpan.FromSeconds(2));

                return written;
            });

        // Act
        TimeProvider.Advance(TimeSpan.FromMilliseconds(50));
        await using var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        var remaining = await ReserveAsync(service, 2);
        await DisposeAttemptsAsync(occupied.Concat(remaining));

        // Assert
        Assert.Null(result);
        Assert.Single(remaining, attempt => attempt is not null);
    }

    [Fact]
    public async Task WaitForLoginAsync_SharedIpIsBusy_ReleasesAccountCapacityBeforeWaiting()
    {
        // Arrange
        var service = GetService<AuthService>();
        var occupied = await Task.WhenAll(Enumerable.Range(0, 15).Select(index =>
            service.TryBeginLoginAsync($"other{index}@exceptionless.test", IpAddress, TestCancellationToken)));
        Assert.All(occupied, Assert.NotNull);

        // Act
        var pending = service.WaitForLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
        var otherIp = await ReserveAsync(service, 5, ipAddress: "192.0.2.2");
        await DisposeAttemptsAsync(otherIp);
        await occupied[0]!.DisposeAsync();
        TimeProvider.Advance(TimeSpan.FromMilliseconds(50));
        await using var result = await pending.WaitAsync(TestTimeout, TestCancellationToken);
        await DisposeAttemptsAsync(occupied);

        // Assert
        Assert.All(otherIp, Assert.NotNull);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WaitForLoginAsync_WaiterLimitReached_DeniesExcessWithoutBlockingAvailableAdmission()
    {
        // Arrange
        var service = GetService<AuthService>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);
        var waiters = Enumerable.Range(0, 100).Select(_ =>
            service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token)).ToArray();

        // Act
        var excess = service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token);
        try
        {
            Assert.All(waiters, waiter => Assert.False(waiter.IsCompleted));
            var denied = await excess.WaitAsync(TestTimeout, TestCancellationToken);
            await using var unrelated = await service.WaitForLoginAsync("other@exceptionless.test", "192.0.2.2", TestCancellationToken);

            // Assert
            Assert.Null(denied);
            Assert.NotNull(unrelated);
            Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), TimeProvider.GetUtcNow());
        }
        finally
        {
            await cancellation.CancelAsync();
            await Record.ExceptionAsync(() => Task.WhenAll(waiters.Append(excess)).WaitAsync(TestTimeout, TestCancellationToken));
            await DisposeAttemptsAsync(occupied);
        }
    }

    [Theory]
    [InlineData("cancellation")]
    [InlineData("deadline")]
    [InlineData("cache failure")]
    public async Task WaitForLoginAsync_WaitersExit_RestoresRetryCapacity(string outcome)
    {
        // Arrange
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(TimeProvider));
        var cacheMock = CreateCacheMock(cache);
        var service = CreateService(cacheMock.Object);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var retryCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var occupied = await ReserveAsync(service, 5);
        Assert.All(occupied, Assert.NotNull);
        var waiters = Enumerable.Range(0, 100).Select(_ =>
            service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token)).ToArray();
        var failure = new IOException("Admission read failed.");
        bool rejectReads = false;
        cacheMock.Setup(client => client.GetAsync<LoginState>(It.IsAny<string>()))
            .Returns((string key) => rejectReads ? Task.FromException<CacheValue<LoginState>>(failure) : cache.GetAsync<LoginState>(key));
        Task<AuthService.LoginAttempt?>[] retries = [];
        var excess = service.WaitForLoginAsync(EmailAddress, IpAddress, cancellation.Token);

        try
        {
            Assert.All(waiters, waiter => Assert.False(waiter.IsCompleted));

            // Act
            var denied = await excess.WaitAsync(TestTimeout, TestCancellationToken);
            _logger.LogInformation("Verifying admission retry capacity after {Outcome}.", outcome);
            switch (outcome)
            {
                case "cancellation":
                    await cancellation.CancelAsync();
                    break;
                case "deadline":
                    TimeProvider.Advance(TimeSpan.FromSeconds(2));
                    break;
                case "cache failure":
                    rejectReads = true;
                    TimeProvider.Advance(TimeSpan.FromMilliseconds(50));
                    break;
            }

            // Assert
            Assert.Null(denied);
            switch (outcome)
            {
                case "cancellation":
                    var cancellationException = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                        Task.WhenAll(waiters).WaitAsync(TestTimeout, TestCancellationToken));
                    Assert.Equal(cancellation.Token, cancellationException.CancellationToken);
                    break;
                case "deadline":
                    var results = await Task.WhenAll(waiters).WaitAsync(TestTimeout, TestCancellationToken);
                    Assert.All(results, Assert.Null);
                    break;
                case "cache failure":
                    var cacheException = await Assert.ThrowsAsync<IOException>(() =>
                        Task.WhenAll(waiters).WaitAsync(TestTimeout, TestCancellationToken));
                    Assert.Same(failure, cacheException);
                    break;
            }

            rejectReads = false;
            await using var stillOccupied = await service.TryBeginLoginAsync(EmailAddress, IpAddress, TestCancellationToken);
            Assert.Null(stillOccupied);
            retries = Enumerable.Range(0, 100).Select(_ =>
                service.WaitForLoginAsync(EmailAddress, IpAddress, retryCancellation.Token)).ToArray();
            Assert.All(retries, retry => Assert.False(retry.IsCompleted));
        }
        finally
        {
            rejectReads = false;
            await cancellation.CancelAsync();
            await retryCancellation.CancelAsync();
            await Record.ExceptionAsync(() => Task.WhenAll(waiters.Concat(retries).Append(excess)).WaitAsync(TestTimeout, TestCancellationToken));
            await DisposeAttemptsAsync(occupied);
        }

        var restored = await ReserveAsync(service, 5);
        await DisposeAttemptsAsync(restored);
        Assert.All(restored, Assert.NotNull);
    }

    private async Task<AuthService.LoginAttempt> BeginAsync(AuthService service, string emailAddress = EmailAddress, string? ipAddress = IpAddress)
    {
        var attempt = await service.TryBeginLoginAsync(emailAddress, ipAddress, TestCancellationToken);
        Assert.NotNull(attempt);
        return attempt;
    }

    private static Mock<ICacheClient> CreateCacheMock(InMemoryCacheClient cache)
    {
        var mock = new Mock<ICacheClient>(MockBehavior.Strict);
        mock.Setup(client => client.GetAsync<LoginState>(It.IsAny<string>()))
            .Returns((string key) => cache.GetAsync<LoginState>(key));
        mock.Setup(client => client.AddAsync(It.IsAny<string>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns((string key, LoginState value, TimeSpan? expiresIn) => cache.AddAsync(key, value, expiresIn));
        mock.Setup(client => client.ReplaceIfEqualAsync(
                It.IsAny<string>(), It.IsAny<LoginState>(), It.IsAny<LoginState>(), It.IsAny<TimeSpan?>()))
            .Returns((string key, LoginState value, LoginState expected, TimeSpan? expiresIn) => cache.ReplaceIfEqualAsync(key, value, expected, expiresIn));
        return mock;
    }

    private AuthService CreateService(ICacheClient cache) => new(cache, TimeProvider, Log);

    private static Task DisposeAttemptsAsync(IEnumerable<AuthService.LoginAttempt?> attempts)
        => Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => attempt!.DisposeAsync().AsTask()));

    private async Task FailAsync(AuthService service, string emailAddress = EmailAddress)
    {
        await using var attempt = await BeginAsync(service, emailAddress);
        await service.RecordLoginFailureAsync(attempt);
    }

    private Task<AuthService.LoginAttempt?[]> ReserveAsync(AuthService service, int count, string emailAddress = EmailAddress, string? ipAddress = IpAddress)
        => Task.WhenAll(Enumerable.Range(0, count).Select(_ => service.TryBeginLoginAsync(emailAddress, ipAddress, TestCancellationToken)));
}
