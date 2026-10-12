using Exceptionless.Core.Services;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class AuthServiceTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentInstances_BoundsChecksBeforeFailuresComplete()
    {
        var first = GetService<AuthService>();
        var second = new AuthService(GetService<ICacheClient>(), TimeProvider);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(i =>
            (i % 2 == 0 ? first : second).TryBeginLoginAsync(" User@exceptionless.test ", null, TestCancellationToken)));
        Assert.Equal(5, attempts.Count(a => a is not null));
        await Task.WhenAll(attempts.Where(a => a is not null).Select(a => first.RecordLoginFailureAsync(a!)));
        Assert.Null(await second.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken));
    }

    [Fact]
    public async Task TryBeginLoginAsync_ConcurrentUsers_BoundsChecksAtSharedIpAddress()
    {
        var service = GetService<AuthService>();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 100).Select(i =>
            service.TryBeginLoginAsync($"user{i}@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        Assert.Equal(15, attempts.Count(a => a is not null));
        await Task.WhenAll(attempts.Where(a => a is not null).Select(a => service.RecordLoginFailureAsync(a!)));
        Assert.Null(await service.TryBeginLoginAsync("other@exceptionless.test", "192.0.2.1", TestCancellationToken));
        // Rejected IP admission must release the partially reserved account slot.
        await using var allowed = await service.TryBeginLoginAsync("user99@exceptionless.test", "192.0.2.2", TestCancellationToken);
        Assert.NotNull(allowed);
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_ValidRequests_DoNotConsumeFailureQuota()
    {
        var service = GetService<AuthService>();
        for (int batch = 0; batch < 20; batch++)
        {
            var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => BeginAsync(service)));
            await Task.WhenAll(attempts.Select(service.RecordLoginSuccessAsync));
            await Task.WhenAll(attempts.Select(a => a.DisposeAsync().AsTask()));
        }
        await using var next = await BeginAsync(service);
    }

    [Fact]
    public async Task DisposeAsync_InterruptedAttempt_ReleasesBothReservations()
    {
        var service = GetService<AuthService>();
        for (int i = 0; i < 30; i++)
            await (await BeginAsync(service)).DisposeAsync();
        await using var next = await BeginAsync(service);
    }

    [Fact]
    public async Task DisposeAsync_CompletedFailure_RetainsCharge()
    {
        var service = GetService<AuthService>();
        for (int i = 0; i < 5; i++)
        {
            await using var attempt = await BeginAsync(service);
            await service.RecordLoginFailureAsync(attempt);
        }
        Assert.Null(await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken));
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_PreservesNewFailuresAndOtherReservations()
    {
        var service = GetService<AuthService>();
        await FailAsync(service);
        var success = await BeginAsync(service);
        var failures = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => BeginAsync(service)));
        await Task.WhenAll(failures.Select(service.RecordLoginFailureAsync));
        await service.RecordLoginSuccessAsync(success);
        await FailAsync(service);
        await FailAsync(service);
        await service.RecordLoginSuccessAsync(success);
        Assert.Null(await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken));
    }

    [Fact]
    public async Task RecordLoginSuccessAsync_DoesNotRefundOtherUsersIpFailures()
    {
        var service = GetService<AuthService>();
        for (int i = 0; i < 14; i++)
            await FailAsync(service, $"other{i}@exceptionless.test");
        await service.RecordLoginSuccessAsync(await BeginAsync(service));
        await FailAsync(service, "last@exceptionless.test");
        Assert.Null(await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken));
    }

    [Fact]
    public async Task ClearUserLoginAttemptsAsync_PreservesIpFailuresAndChecksUnderway()
    {
        var service = GetService<AuthService>();
        for (int i = 0; i < 4; i++)
            await FailAsync(service);
        var pending = await BeginAsync(service);
        await service.ClearUserLoginAttemptsAsync(" User@exceptionless.test ");
        var remaining = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken)));
        Assert.Equal(4, remaining.Count(a => a is not null));
        await pending.DisposeAsync();
        await Task.WhenAll(remaining.Where(a => a is not null).Select(a => a!.DisposeAsync().AsTask()));
        for (int i = 0; i < 11; i++)
            await FailAsync(service, $"other{i}@exceptionless.test");
        Assert.Null(await service.TryBeginLoginAsync("user@exceptionless.test", "192.0.2.1", TestCancellationToken));
    }

    [Fact]
    public async Task TryBeginLoginAsync_QuarterHour_ExpiresFailuresAndAbandonedReservations()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 14, 0, TimeSpan.Zero));
        var service = GetService<AuthService>();
        var old = await BeginAsync(service);
        for (int i = 0; i < 4; i++)
            await FailAsync(service);
        Assert.Null(await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken));
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        for (int i = 0; i < 5; i++)
            await FailAsync(service);
        await service.RecordLoginSuccessAsync(old);
        await service.RecordLoginFailureAsync(old);
        Assert.Null(await service.TryBeginLoginAsync("user@exceptionless.test", null, TestCancellationToken));
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await using var next = await BeginAsync(service);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryBeginLoginAsync_InvalidEmail_Throws(string? email)
    {
        var service = GetService<AuthService>();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.TryBeginLoginAsync(email!, null, TestCancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.ClearUserLoginAttemptsAsync(email!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public Task TryBeginLoginAsync_InvalidIpAddress_Throws(string address)
        => Assert.ThrowsAnyAsync<ArgumentException>(() => GetService<AuthService>().TryBeginLoginAsync("user@example.test", address, TestCancellationToken));

    [Fact]
    public async Task TryBeginLoginAsync_CancelledRequest_Throws()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => GetService<AuthService>().TryBeginLoginAsync("user@example.test", null, cancellation.Token));
    }

    [Fact]
    public async Task RecordLoginAsync_NullAttempt_Throws()
    {
        var service = GetService<AuthService>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordLoginFailureAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.RecordLoginSuccessAsync(null!));
        Assert.Throws<ArgumentNullException>(() => new AuthService(null!, TimeProvider));
        Assert.Throws<ArgumentNullException>(() => new AuthService(GetService<ICacheClient>(), null!));
    }

    private async Task<AuthService.LoginAttempt> BeginAsync(AuthService service, string email = "user@exceptionless.test")
    {
        var attempt = await service.TryBeginLoginAsync(email, "192.0.2.1", TestCancellationToken);
        Assert.NotNull(attempt);
        return attempt;
    }

    private async Task FailAsync(AuthService service, string email = "user@exceptionless.test")
    {
        await using var attempt = await BeginAsync(service, email);
        await service.RecordLoginFailureAsync(attempt);
    }
}
