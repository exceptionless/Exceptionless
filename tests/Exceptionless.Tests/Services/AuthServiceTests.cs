using Exceptionless.Core.Services;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public async Task TryBeginLoginAsync_FiveFailures_SharesLimitAcrossInstancesAndNormalizedEmails()
    {
        // Arrange
        var first = GetService<AuthService>();
        var second = new AuthService(GetService<ICacheClient>(), TimeProvider);
        for (int failure = 0; failure < 5; failure++)
            await FailLoginAsync(first, " User@exceptionless.test ", $"192.0.2.{failure}");

        // Act
        var attempt = await second.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.100", TestCancellationToken);

        // Assert
        Assert.Null(attempt);
    }

    [Fact]
    public async Task TryBeginLoginAsync_MissingIpAddress_OnlyLimitsFailingUser()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int failure = 0; failure < 5; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", null);

        // Act
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);
        var otherUsers = await Task.WhenAll(Enumerable.Range(0, 30).Select(user =>
            service.TryBeginLoginAsync($"other{user}@exceptionless.test", null, TestCancellationToken)));

        // Assert
        Assert.Null(blocked);
        Assert.All(otherUsers, Assert.NotNull);
    }

    [Fact]
    public async Task TryBeginLoginAsync_FifteenIpFailures_BlocksOtherUsersOnlyAtThatAddress()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int failure = 0; failure < 15; failure++)
            await FailLoginAsync(service, $"user{failure}@exceptionless.test", "192.0.2.1");

        // Act
        var blocked = await service.TryBeginLoginAsync("another@exceptionless.test", "192.0.2.1", TestCancellationToken);
        var allowed = await service.TryBeginLoginAsync("another@exceptionless.test", "192.0.2.2", TestCancellationToken);

        // Assert
        Assert.Null(blocked);
        Assert.NotNull(allowed);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ConcurrentValidAttempts_AllRemainAllowed()
    {
        // Arrange
        var service = GetService<AuthService>();
        var cache = Assert.IsType<InMemoryCacheClient>(GetService<ICacheClient>());
        long originalWrites = cache.Writes;
        // Begin every request before any completes to reproduce the former reservation limit.
        var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(request =>
            service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken)));

        // Act
        await Task.WhenAll(attempts.Where(attempt => attempt is not null).Select(attempt => service.RecordLoginSuccessAsync(attempt!)));
        var nextAttempt = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.All(attempts, Assert.NotNull);
        Assert.NotNull(nextAttempt);
        Assert.Equal(originalWrites, cache.Writes);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_ConcurrentFailures_AreNotLostAcrossInstances()
    {
        // Arrange
        var first = GetService<AuthService>();
        var second = new AuthService(GetService<ICacheClient>(), TimeProvider);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(request =>
            first.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken)));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failures = attempts.Select(attempt => Task.Run(async () =>
        {
            await start.Task;
            Assert.NotNull(attempt);
            await second.RecordLoginFailureAsync(attempt);
        }, TestCancellationToken)).ToArray();

        // Act
        start.SetResult();
        await Task.WhenAll(failures);
        var blocked = await first.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);

        // Assert
        Assert.Null(blocked);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_ConcurrentIpFailures_BlockSubsequentRequests()
    {
        // Arrange
        var service = GetService<AuthService>();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(user =>
            service.TryBeginLoginAsync($"user{user}@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failures = attempts.Select(attempt => Task.Run(async () =>
        {
            await start.Task;
            Assert.NotNull(attempt);
            await service.RecordLoginFailureAsync(attempt);
        }, TestCancellationToken)).ToArray();

        // Act
        start.SetResult();
        await Task.WhenAll(failures);
        var blocked = await service.TryBeginLoginAsync("another@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.All(attempts, Assert.NotNull);
        Assert.Null(blocked);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_OnlyClearsObservedUserFailures()
    {
        // Arrange
        var service = GetService<AuthService>();
        await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");
        var success = await BeginLoginAsync(service);
        for (int failure = 0; failure < 4; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");

        // Act
        await service.RecordLoginSuccessAsync(success);
        await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");
        // Repeated completion must not remove the new failure that reused the cleared slot.
        await service.RecordLoginSuccessAsync(success);
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Null(blocked);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ConcurrentFailures_PreservesEveryNewFailure()
    {
        // Arrange
        var service = GetService<AuthService>();
        await FailLoginAsync(service, "user@exceptionless.test", null);
        var successes = await Task.WhenAll(Enumerable.Range(0, 20).Select(request =>
            BeginLoginAsync(service, "user@exceptionless.test", null)));
        var failures = await Task.WhenAll(Enumerable.Range(0, 4).Select(request =>
            BeginLoginAsync(service, "user@exceptionless.test", null)));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completions = successes.Select(attempt => Task.Run(async () =>
        {
            await start.Task;
            await service.RecordLoginSuccessAsync(attempt);
        }, TestCancellationToken)).Concat(failures.Select(attempt => Task.Run(async () =>
        {
            await start.Task;
            await service.RecordLoginFailureAsync(attempt);
        }, TestCancellationToken))).ToArray();

        // Act
        start.SetResult();
        await Task.WhenAll(completions);
        await FailLoginAsync(service, "user@exceptionless.test", null);
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken);

        // Assert
        Assert.Null(blocked);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_DoesNotRefundIpFailures()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int failure = 0; failure < 14; failure++)
            await FailLoginAsync(service, $"other{failure}@exceptionless.test", "192.0.2.1");
        var success = await BeginLoginAsync(service);

        // Act
        await service.RecordLoginSuccessAsync(success);
        await FailLoginAsync(service, "last@exceptionless.test", "192.0.2.1");
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Null(blocked);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_OldWindow_PreservesNewWindowFailures()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 14, 0, TimeSpan.Zero));
        var service = GetService<AuthService>();
        await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");
        var success = await BeginLoginAsync(service);
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        for (int failure = 0; failure < 5; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");

        // Act
        await service.RecordLoginSuccessAsync(success);
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Null(blocked);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_OldWindow_DoesNotChargeNewWindow()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 14, 0, TimeSpan.Zero));
        var service = GetService<AuthService>();
        var oldAttempt = await BeginLoginAsync(service);
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        for (int failure = 0; failure < 4; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");

        // Act
        await service.RecordLoginFailureAsync(oldAttempt);
        var allowed = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.NotNull(allowed);
    }

    [Fact]
    public async Task TryBeginLoginAsync_AtQuarterHour_StartsFullNewWindow()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 14, 0, TimeSpan.Zero));
        var service = GetService<AuthService>();
        for (int failure = 0; failure < 5; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");
        TimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Act
        for (int failure = 0; failure < 5; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        var allowed = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.Null(blocked);
        Assert.NotNull(allowed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryBeginLoginAsync_BlockedRetries_DoNotExtendWindow(bool limitByIpAddress)
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        var service = GetService<AuthService>();
        int failureLimit = limitByIpAddress ? 15 : 5;
        for (int failure = 0; failure < failureLimit; failure++)
        {
            string emailAddress = limitByIpAddress ? $"other{failure}@exceptionless.test" : "user@exceptionless.test";
            await FailLoginAsync(service, emailAddress, "192.0.2.1");
        }
        TimeProvider.Advance(TimeSpan.FromMinutes(13));

        // Act
        var blockedRetries = await Task.WhenAll(Enumerable.Range(0, 20).Select(request =>
            service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var allowed = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);

        // Assert
        Assert.All(blockedRetries, Assert.Null);
        Assert.NotNull(allowed);
    }

    [Fact]
    public async Task ClearUserLoginAttemptsAsync_Recovery_ClearsUserLimitButPreservesIpLimit()
    {
        // Arrange
        var service = GetService<AuthService>();
        for (int failure = 0; failure < 5; failure++)
            await FailLoginAsync(service, "user@exceptionless.test", "192.0.2.1");
        for (int failure = 0; failure < 10; failure++)
            await FailLoginAsync(service, $"other{failure}@exceptionless.test", "192.0.2.1");

        // Act
        await service.ClearUserLoginAttemptsAsync("user@exceptionless.test");
        var blocked = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken);
        var allowed = await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.2", TestCancellationToken);

        // Assert
        Assert.Null(blocked);
        Assert.NotNull(allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryBeginLoginAsync_InvalidEmailAddress_Throws(string? emailAddress)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.TryBeginLoginAsync(emailAddress!, null, TestCancellationToken));

        // Assert
        Assert.Equal("emailAddress", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryBeginLoginAsync_InvalidIpAddress_Throws(string ipAddress)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.TryBeginLoginAsync("user@exceptionless.test", ipAddress, TestCancellationToken));

        // Assert
        Assert.Equal("ipAddress", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ClearUserLoginAttemptsAsync_InvalidEmailAddress_Throws(string? emailAddress)
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.ClearUserLoginAttemptsAsync(emailAddress!));

        // Assert
        Assert.Equal("emailAddress", exception.ParamName);
    }

    [Fact]
    public async Task RecordLoginFailureAsync_NullAttempt_Throws()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordLoginFailureAsync(null!));

        // Assert
        Assert.Equal("attempt", exception.ParamName);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_NullAttempt_Throws()
    {
        // Arrange
        var service = GetService<AuthService>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordLoginSuccessAsync(null!));

        // Assert
        Assert.Equal("attempt", exception.ParamName);
    }

    [Fact]
    public async Task TryBeginLoginAsync_CanceledRequest_Throws()
    {
        // Arrange
        var service = GetService<AuthService>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        await cancellation.CancelAsync();

        // Act
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => service.TryBeginLoginAsync("user@exceptionless.test", null, cancellation.Token));

        // Assert
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public void Constructor_NullCache_Throws()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new AuthService(null!, TimeProvider));

        // Assert
        Assert.Equal("cacheClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullTimeProvider_Throws()
    {
        // Arrange
        var cache = GetService<ICacheClient>();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new AuthService(cache, null!));

        // Assert
        Assert.Equal("timeProvider", exception.ParamName);
    }

    private async Task<AuthService.LoginAttempt> BeginLoginAsync(AuthService service, string emailAddress = "user@exceptionless.test", string? ipAddress = "192.0.2.1")
    {
        var attempt = await service.TryBeginLoginAsync(emailAddress, ipAddress, TestCancellationToken);
        Assert.NotNull(attempt);
        return attempt;
    }

    private async Task FailLoginAsync(AuthService service, string emailAddress, string? ipAddress)
    {
        var attempt = await BeginLoginAsync(service, emailAddress, ipAddress);
        await service.RecordLoginFailureAsync(attempt);
    }
}
