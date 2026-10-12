using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Security;

public sealed class BasicPasswordAuthenticationTests(ITestOutputHelper output) : TestWithServices(output)
{
    private const string EmailAddress = "user@exceptionless.test";
    private const string Password = "Password:1$";
    private const string Salt = "1234567890123456";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false, true, 5)]
    [InlineData(true, true, 15)]
    [InlineData(false, false, 5)]
    [InlineData(true, false, 15)]
    public async Task AuthenticateAsync_ConcurrentBasicRequests_PreservesValidBurstsAndBoundsFailedChecks(bool differentUsers, bool validPassword, int limit)
    {
        // Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        var service = new AuthService(cache, clock, Log);
        var repository = new Mock<IUserRepository>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string passwordHash = Password.ToSaltedHash(Salt);
        int requestCount = limit + 1;
        repository.Setup(repository => repository.GetByEmailAddressAsync(It.IsAny<string>()))
            .Returns(async (string email) =>
            {
                await release.Task.WaitAsync(TestTimeout, TestCancellationToken);
                return new User
                {
                    Id = "123456789012345678901234",
                    EmailAddress = email,
                    FullName = "Admission Test User",
                    IsEmailAddressVerified = true,
                    Password = passwordHash,
                    Salt = Salt,
                    Roles = new HashSet<string> { AuthorizationRoles.User }
                };
            });

        // Act
        var requests = Enumerable.Range(0, requestCount).Select(index => AuthenticateAsync(
            differentUsers ? $"user{index}@exceptionless.test" : EmailAddress,
            validPassword ? Password : "wrong-password", repository.Object, service, cache, clock, TestCancellationToken)).ToArray();
        int initiallyAdmitted = repository.Invocations.Count;
        bool allWaiting = requests.All(request => !request.IsCompleted);
        release.TrySetResult();
        await Task.WhenAll(requests.Take(limit)).WaitAsync(TestTimeout, TestCancellationToken);
        // One request crosses the actual user/IP admission boundary. Complete the
        // admitted checks before advancing the waiter, avoiding scheduler-dependent waves.
        clock.Advance(TimeSpan.FromMilliseconds(50));
        var results = await Task.WhenAll(requests).WaitAsync(TestTimeout, TestCancellationToken);

        // Assert
        Assert.Equal(limit, initiallyAdmitted);
        Assert.True(allWaiting);
        Assert.All(results, result => Assert.Equal(validPassword, result.Succeeded));
        repository.Verify(repository => repository.GetByEmailAddressAsync(It.IsAny<string>()), Times.Exactly(validPassword ? requestCount : limit));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    public async Task AuthenticateAsync_RepositoryException_LogsUnexpectedFailureAndReleasesAdmission(bool cancellationException, bool cancelRequest, int expectedErrors)
    {
        // Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        using var cache = new InMemoryCacheClient(options => options.TimeProvider(clock));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var service = new AuthService(cache, clock, Log);
        var repository = new Mock<IUserRepository>();
        Exception failure = cancellationException ? new OperationCanceledException(cancellation.Token) : new IOException("Synthetic repository failure.");
        repository.Setup(repository => repository.GetByEmailAddressAsync(EmailAddress))
            .Callback(() =>
            {
                if (cancelRequest)
                    cancellation.Cancel();
            })
            .ThrowsAsync(failure);

        // Act
        var result = await AuthenticateAsync(EmailAddress, Password, repository.Object, service, cache, clock, cancellation.Token);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 15).Select(index => service.TryBeginLoginAsync(
            index < 5 ? EmailAddress : $"other{index}@exceptionless.test", "192.0.2.1", TestCancellationToken)));
        await Task.WhenAll(attempts.OfType<AuthService.LoginAttempt>().Select(attempt => attempt.DisposeAsync().AsTask()));

        // Assert
        Assert.Same(failure, result.Failure);
        Assert.False(result.Succeeded);
        Assert.All(attempts, Assert.NotNull);
        var errors = Log.LogEntries.Where(entry => entry.CategoryName == typeof(ApiKeyAuthenticationHandler).FullName && entry.LogLevel >= LogLevel.Error).ToArray();
        Assert.Equal(expectedErrors, errors.Length);
        Assert.All(errors, entry => Assert.Same(failure, entry.Exception));
        repository.Verify(repository => repository.GetByEmailAddressAsync(EmailAddress), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);
        services.AddSingleton(options.OAuthServerOptions);
    }

    private async Task<AuthenticateResult> AuthenticateAsync(string emailAddress, string password, IUserRepository repository, AuthService service,
        ICacheClient cache, TimeProvider clock, CancellationToken cancellationToken)
    {
        var options = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        options.Setup(monitor => monitor.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationOptions());
        var handler = new ApiKeyAuthenticationHandler(
            GetService<ITokenRepository>(), GetService<IOAuthTokenRepository>(), cache, service, repository,
            GetService<OAuthService>(), GetService<AppOptions>(), options.Object, clock, Log, UrlEncoder.Default);
        var context = new DefaultHttpContext
        {
            RequestServices = GetService<IServiceProvider>(),
            RequestAborted = cancellationToken
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        context.Request.Path = "/api/v2/users/me";
        context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{emailAddress}:{password}"));
        await handler.InitializeAsync(new AuthenticationScheme(ApiKeyAuthenticationOptions.ApiKeySchema, null, typeof(ApiKeyAuthenticationHandler)), context);
        return await handler.AuthenticateAsync();
    }
}
