using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Security;

public sealed class ApiKeyAuthenticationHandlerTests(ITestOutputHelper output) : TestWithServices(output)
{
    private const string EmailAddress = "user@exceptionless.test";
    private const string Password = "Password:1$";
    private const string Salt = "1234567890123456";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "previous")]
    [InlineData(false, "current")]
    public async Task AuthenticateAsync_OAuthCachedUser_RejectsMismatchWithoutRevokingActiveUserToken(bool isActive, string? cachedVersion)
    {
        // Arrange
        const string tokenId = "oauth-token";
        const string clientId = "generation-test-client";
        const string refreshHash = "valid-refresh-hash";
        string accessToken = new('a', OAuthService.OAuthTokenLength);
        var user = new User { Id = "123456789012345678901234", IsActive = isActive, AuthenticationVersion = cachedVersion };
        var appOptions = GetService<AppOptions>();
        string origin = (String.IsNullOrWhiteSpace(appOptions.ApiUrl) ? appOptions.BaseURL : appOptions.ApiUrl).GetOrigin();
        var token = new OAuthToken
        {
            Id = tokenId, UserId = user.Id, ClientId = clientId, AuthenticationVersion = "current",
            AccessTokenHash = OAuthService.CreateTokenHash(accessToken), RefreshTokenHash = refreshHash,
            Resource = OAuthService.CreateResourceUri(origin, OAuthService.McpResource), OrganizationIds = ["organization"]
        };
        var cache = GetService<ICacheClient>();
        await cache.SetAsync($"oauth:access:{token.AccessTokenHash}", tokenId);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback<Id, CommandOptionsDescriptor<User>>((_, configure) =>
            {
                var commandOptions = new CommandOptions<User>();
                configure(commandOptions);
                Assert.True(commandOptions.ShouldReadCache());
            }).ReturnsAsync(user);
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        var oauthTokens = new Mock<IOAuthTokenRepository>(MockBehavior.Strict);
        oauthTokens.Setup(repository => repository.GetByIdAsync(tokenId, It.IsAny<CommandOptionsDescriptor<OAuthToken>>())).ReturnsAsync(token);
        oauthTokens.Setup(repository => repository.SaveAsync(token, It.IsAny<CommandOptionsDescriptor<OAuthToken>>())).ReturnsAsync(token);
        var applications = new Mock<IOAuthApplicationRepository>(MockBehavior.Strict);
        applications.Setup(repository => repository.GetByClientIdAsync(clientId, null)).ReturnsAsync(new OAuthApplication { ClientId = clientId });
        var oauthService = new OAuthService(new OAuthServerOptions(), cache, GetService<ILockProvider>(), applications.Object,
            GetService<IOAuthClientMetadataService>(), oauthTokens.Object, users.Object, TimeProvider);
        var options = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>(MockBehavior.Strict);
        options.Setup(monitor => monitor.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationOptions());
        var handler = new ApiKeyAuthenticationHandler(tokens.Object, oauthTokens.Object, cache, GetService<AuthService>(), users.Object,
            oauthService, appOptions, options.Object, TimeProvider, Log, UrlEncoder.Default);
        var context = new DefaultHttpContext { RequestServices = GetService<IServiceProvider>() };
        context.Request.Path = OAuthService.McpResource.Path;
        context.Request.Headers.Authorization = $"Bearer {accessToken}";
        await handler.InitializeAsync(new AuthenticationScheme(ApiKeyAuthenticationOptions.ApiKeySchema, null, typeof(ApiKeyAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal(!isActive, token.IsDisabled);
        Assert.Equal(isActive ? refreshHash : null, token.RefreshTokenHash);
        oauthTokens.Verify(repository => repository.SaveAsync(token, It.IsAny<CommandOptionsDescriptor<OAuthToken>>()), Times.Exactly(isActive ? 0 : 1));
        oauthTokens.Verify(repository => repository.GetByIdAsync(tokenId, It.IsAny<CommandOptionsDescriptor<OAuthToken>>()), Times.Once);
        users.Verify(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>()), Times.Once);
        applications.Verify(repository => repository.GetByClientIdAsync(clientId, null), Times.Once);
        oauthTokens.VerifyNoOtherCalls();
        users.VerifyNoOtherCalls();
        applications.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Bearer", true, null, null, true)]
    [InlineData("Basic", true, null, null, true)]
    [InlineData("Bearer", true, "current", null, false)]
    [InlineData("Basic", true, "current", null, false)]
    [InlineData("Bearer", true, "current", "CURRENT", false)]
    [InlineData("Basic", true, "current", "CURRENT", false)]
    [InlineData("Bearer", false, "current", "current", false)]
    [InlineData("Basic", false, "current", "current", false)]
    [InlineData("Bearer", true, "current", "current", true)]
    [InlineData("Basic", true, "current", "current", true)]
    public async Task AuthenticateAsync_AuthenticationVersion_RequiresActiveUserAndMatchingGeneration(string scheme, bool isActive, string? userVersion, string? tokenVersion, bool expectedSuccess)
    {
        // Arrange
        const string tokenId = "legacy-user-token";
        var user = new User
        {
            Id = "123456789012345678901234",
            EmailAddress = EmailAddress,
            FullName = "Token Test User",
            IsActive = isActive,
            AuthenticationVersion = userVersion,
            Roles = new HashSet<string> { AuthorizationRoles.User }
        };
        var token = new Token { Id = tokenId, UserId = user.Id, Type = TokenType.Authentication, AuthenticationVersion = tokenVersion };
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback<Id, CommandOptionsDescriptor<User>>((_, configure) =>
            {
                var commandOptions = new CommandOptions<User>();
                configure(commandOptions);
                Assert.True(commandOptions.ShouldReadCache());
            })
            .ReturnsAsync(user);
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        tokens.Setup(repository => repository.GetByIdAsync(tokenId, It.IsAny<CommandOptionsDescriptor<Token>>())).ReturnsAsync(token);
        var oauthTokens = new Mock<IOAuthTokenRepository>(MockBehavior.Strict);
        var options = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>(MockBehavior.Strict);
        options.Setup(monitor => monitor.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationOptions());
        var handler = new ApiKeyAuthenticationHandler(tokens.Object, oauthTokens.Object, GetService<ICacheClient>(), GetService<AuthService>(), users.Object,
            GetService<OAuthService>(), GetService<AppOptions>(), options.Object, TimeProvider, Log, UrlEncoder.Default);
        var context = new DefaultHttpContext { RequestServices = GetService<IServiceProvider>() };
        context.Request.Path = "/api/v2/users/me";
        string credentials = String.Equals(scheme, "Basic", StringComparison.Ordinal)
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"client:{tokenId}")) : tokenId;
        context.Request.Headers.Authorization = $"{scheme} {credentials}";
        await handler.InitializeAsync(new AuthenticationScheme(ApiKeyAuthenticationOptions.ApiKeySchema, null, typeof(ApiKeyAuthenticationHandler)), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        Assert.Equal(expectedSuccess, result.Succeeded);
        users.Verify(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>()), Times.Once);
        tokens.Verify(repository => repository.GetByIdAsync(tokenId, It.IsAny<CommandOptionsDescriptor<Token>>()), Times.Exactly(2));
        users.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
        oauthTokens.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, true, 5)]
    [InlineData(true, true, 15)]
    [InlineData(false, false, 5)]
    [InlineData(true, false, 15)]
    public async Task AuthenticateAsync_OverlappingBasicRequests_PreservesValidBurstsAndBoundsFailedChecks(bool differentUsers, bool validPassword, int limit)
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        var cache = GetService<ICacheClient>();
        var service = GetService<AuthService>();
        var repository = new Mock<IUserRepository>();
        string passwordHash = Password.ToSaltedHash(Salt);
        int requestCount = limit + 1;
        var releases = Enumerable.Range(0, requestCount)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        int repositoryCalls = 0;
        repository.Setup(repository => repository.GetByEmailAddressAsync(It.IsAny<string>()))
            .Returns(async (string email) =>
            {
                int call = Interlocked.Increment(ref repositoryCalls) - 1;
                await releases[call].Task.WaitAsync(TestTimeout, TestCancellationToken);
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
            validPassword ? Password : "wrong-password", repository.Object, service, cache, TimeProvider, TestCancellationToken)).ToArray();
        int initiallyAdmitted = repository.Invocations.Count;
        bool allWaiting = requests.All(request => !request.IsCompleted);
        // Keep requests overlapping at admission, with controlled completion order.
        // Simultaneous provider writes are tracked separately in #2655.
        for (int index = 0; index < limit; index++)
        {
            releases[index].TrySetResult();
            await requests[index].WaitAsync(TestTimeout, TestCancellationToken);
        }

        releases[limit].TrySetResult();
        TimeProvider.Advance(TimeSpan.FromMilliseconds(50));
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
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero));
        var cache = GetService<ICacheClient>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var service = GetService<AuthService>();
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
        var result = await AuthenticateAsync(EmailAddress, Password, repository.Object, service, cache, TimeProvider, cancellation.Token);
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
