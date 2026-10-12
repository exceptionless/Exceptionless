using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
        var service = new AuthService(cache, clock, NullLogger<AuthService>.Instance);
        var repository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string passwordHash = Password.ToSaltedHash(Salt);
        int lookups = 0;
        int requestCount = limit + 1;
        ((UserRepositoryProxy)(object)repository).Lookup = async email =>
        {
            Interlocked.Increment(ref lookups);
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
        };

        // Act
        var requests = Enumerable.Range(0, requestCount).Select(index => AuthenticateAsync(
            differentUsers ? $"user{index}@exceptionless.test" : EmailAddress,
            validPassword ? Password : "wrong-password", repository, service, cache, clock, TestCancellationToken)).ToArray();
        int initiallyAdmitted = Volatile.Read(ref lookups);
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
        Assert.Equal(validPassword ? requestCount : limit, Volatile.Read(ref lookups));
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    public async Task AuthenticateAsync_RepositoryException_LogsUnexpectedFailureAndReleasesAdmission(bool cancellationException, bool cancelRequest, int expectedErrors)
    {
        // Arrange
        using var cache = new InMemoryCacheClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        using var logger = new CapturingLoggerFactory();
        var service = new AuthService(cache, System.TimeProvider.System, NullLogger<AuthService>.Instance);
        var repository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        Exception failure = cancellationException ? new OperationCanceledException(cancellation.Token) : new IOException("Synthetic repository failure.");
        ((UserRepositoryProxy)(object)repository).Lookup = _ =>
        {
            if (cancelRequest)
                cancellation.Cancel();

            return Task.FromException<User?>(failure);
        };

        // Act
        var result = await AuthenticateAsync(EmailAddress, Password, repository, service, cache, System.TimeProvider.System, cancellation.Token, logger);

        // Assert
        Assert.Same(failure, result.Failure);
        Assert.False(result.Succeeded);
        Assert.Empty(cache.Keys);
        Assert.Equal(expectedErrors, logger.Errors.Count);
        Assert.All(logger.Errors, exception => Assert.Same(failure, exception));
    }

    private async Task<AuthenticateResult> AuthenticateAsync(string emailAddress, string password, IUserRepository repository, AuthService service,
        ICacheClient cache, TimeProvider clock, CancellationToken cancellationToken, ILoggerFactory? logger = null)
    {
        var handler = new ApiKeyAuthenticationHandler(
            GetService<ITokenRepository>(), GetService<IOAuthTokenRepository>(), cache, service, repository,
            GetService<OAuthService>(), GetService<AppOptions>(), new TestOptionsMonitor(), clock,
            logger ?? NullLoggerFactory.Instance, UrlEncoder.Default);
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

    private sealed class CapturingLoggerFactory : ILoggerFactory, ILogger
    {
        public ConcurrentQueue<Exception?> Errors { get; } = new();
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                Errors.Enqueue(exception);
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<ApiKeyAuthenticationOptions>
    {
        public ApiKeyAuthenticationOptions CurrentValue { get; } = new();
        public ApiKeyAuthenticationOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ApiKeyAuthenticationOptions, string?> listener) => null;
    }

    private class UserRepositoryProxy : DispatchProxy
    {
        public Func<string, Task<User?>> Lookup { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IUserRepository.GetByEmailAddressAsync))
                return Lookup((string)args![0]!);

            throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
        }
    }
}
