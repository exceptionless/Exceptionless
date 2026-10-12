using System.Reflection;
using System.Runtime.ExceptionServices;
using Exceptionless.Core.Services;
using Foundatio.Caching;
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
    public async Task RecordLoginAsync_NullAttempt_Throws()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var failureException = await Record.ExceptionAsync(() => service.RecordLoginFailureAsync(null!));
        var successException = await Record.ExceptionAsync(() => service.RecordLoginSuccessAsync(null!));
        var cacheException = Record.Exception(() => new AuthService(null!, TimeProvider));
        var timeException = Record.Exception(() => new AuthService(GetService<ICacheClient>(), null!));

        // Assert
        Assert.IsType<ArgumentNullException>(failureException);
        Assert.IsType<ArgumentNullException>(successException);
        Assert.IsType<ArgumentNullException>(cacheException);
        Assert.IsType<ArgumentNullException>(timeException);
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
        var cache = CacheCallProxy.Create(GetService<ICacheClient>(), out var proxy);
        string? ipAddress = includeIpAddress ? "192.0.2.1" : null;
        proxy.AfterAdd = cacheKey =>
        {
            if (cacheKey.Contains(includeIpAddress ? "ip:" : "user:", StringComparison.Ordinal))
                cancellation.Cancel();
        };

        var service = new AuthService(cache, TimeProvider);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@exceptionless.test", ipAddress, cancellation.Token);
        });
        proxy.AfterAdd = null;
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

    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentInstances_BoundsChecksBeforeFailuresComplete()
    {
        // Arrange
        var first = GetService<AuthService>();
        var second = new AuthService(GetService<ICacheClient>(), TimeProvider);

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
        var cache = CacheCallProxy.Create(GetService<ICacheClient>(), out var proxy);
        var failure = new InvalidOperationException("Synthetic cache failure.");
        proxy.BeforeAdd = cacheKey =>
        {
            if (cacheKey.Contains("ip:", StringComparison.Ordinal))
                throw failure;
        };

        var service = new AuthService(cache, TimeProvider);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);
        });
        proxy.BeforeAdd = null;
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

    public class CacheCallProxy : DispatchProxy
    {
        private ICacheClient _inner = null!;
        public Action<string>? BeforeAdd { get; set; }
        public Action<string>? AfterAdd { get; set; }

        public static ICacheClient Create(ICacheClient inner, out CacheCallProxy proxy)
        {
            var cache = Create<ICacheClient, CacheCallProxy>();
            proxy = (CacheCallProxy)cache;
            proxy._inner = inner;

            return cache;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(ICacheClient.AddAsync))
            {
                string cacheKey = (string)args![0]!;
                BeforeAdd?.Invoke(cacheKey);

                return AddAsync(targetMethod, args, cacheKey);
            }

            return InvokeInner(targetMethod, args);
        }

        private async Task<bool> AddAsync(MethodInfo method, object?[] args, string cacheKey)
        {
            bool added = await (Task<bool>)InvokeInner(method, args)!;
            if (added)
                AfterAdd?.Invoke(cacheKey);

            return added;
        }

        private object? InvokeInner(MethodInfo method, object?[]? args)
        {
            try
            {
                return method.Invoke(_inner, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
