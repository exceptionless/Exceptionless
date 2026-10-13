using System.Reflection;
using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class AuthHandlerTests : TestWithServices
{
    public AuthHandlerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "previous-version")]
    [InlineData(true, "previous-version")]
    public async Task Handle_ExternalLoginTransfer_RevokesDisplacedUserSessionsBeforeLinking(bool cleanupFails, string? previousVersion)
    {
        // Arrange
        const string provider = "github";
        const string providerId = "provider-user";
        var displaced = new User { Id = "displaced-user", EmailAddress = "displaced@example.test", AuthenticationVersion = previousVersion };
        displaced.AddOAuthAccount(provider, providerId, displaced.EmailAddress);
        displaced.AddOAuthAccount("google", "retained-provider", displaced.EmailAddress);
        var current = new User { Id = "current-user", EmailAddress = "current@example.test", AuthenticationVersion = "current-version" };
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(IdentityUtils.UserAuthenticationType)) };
        context.Request.SetUser(current);
        var calls = new List<string>();
        string? persistedVersion = null;
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetUserByOAuthProviderAsync(provider, providerId)).ReturnsAsync(displaced);
        users.Setup(repository => repository.SaveAsync(displaced, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback(() => { persistedVersion = displaced.AuthenticationVersion; calls.Add("save-displaced"); }).ReturnsAsync(displaced);
        users.Setup(repository => repository.SaveAsync(current, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback(() => calls.Add("save-current")).ReturnsAsync(current);
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        tokens.Setup(repository => repository.RemoveAllByUserIdAsync(displaced.Id, null)).Returns(() =>
        {
            calls.Add("remove-tokens");
            return cleanupFails ? Task.FromException<long>(new IOException("Token store unavailable.")) : Task.FromResult(1L);
        });
        tokens.Setup(repository => repository.GetByTypeAndUserIdAsync(TokenType.Authentication, current.Id, null)).ReturnsAsync(FindResults<Token>.Empty);
        tokens.Setup(repository => repository.AddAsync(It.IsAny<Token>(), It.IsAny<CommandOptionsDescriptor<Token>>()))
            .ReturnsAsync((Token token, CommandOptionsDescriptor<Token> _) => token);
        var oauthTokens = new Mock<IOAuthTokenRepository>(MockBehavior.Strict);
        oauthTokens.Setup(repository => repository.RemoveAllByUserIdAsync(displaced.Id, null))
            .Callback(() => calls.Add("remove-oauth-tokens")).ReturnsAsync(1L);
        var providerClient = new Mock<IOAuthProviderClient>(MockBehavior.Strict);
        providerClient.Setup(client => client.GetGitHubUserInfoAsync(It.IsAny<ExternalAuthInfo>(), "test-client", "test-secret"))
            .ReturnsAsync(new OAuth2.Models.UserInfo { ProviderName = provider, Id = providerId, Email = displaced.EmailAddress });
        var options = GetService<AppOptions>().AuthOptions;
        options.GitHubId = "test-client";
        options.GitHubSecret = "test-secret";
        var handler = CreateHandler(users.Object, tokens.Object, oauthTokens.Object, providerClient.Object);

        // Act
        var result = await handler.Handle(new GitHubLogin(new ExternalAuthInfo(), context));

        // Assert
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.False(String.IsNullOrEmpty(persistedVersion));
        Assert.False(String.Equals(previousVersion, persistedVersion, StringComparison.Ordinal));
        Assert.Equal(["save-displaced", "remove-tokens", "remove-oauth-tokens", "save-current"], calls);
        Assert.DoesNotContain(displaced.OAuthAccounts, account => String.Equals(account.ProviderUserId, providerId, StringComparison.Ordinal));
        Assert.Contains(current.OAuthAccounts, account => String.Equals(account.ProviderUserId, providerId, StringComparison.Ordinal));
        Assert.Equal("current-version", current.AuthenticationVersion);
        users.VerifyAll();
        tokens.VerifyAll();
        oauthTokens.VerifyAll();
        providerClient.VerifyAll();
    }

    [Theory]
    [MemberData(nameof(LoginRepositoryExceptions))]
    public async Task Handle_LoginRepositoryException_ReturnsUnauthorizedResult(Exception exception)
    {
        // Arrange
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAddressAsync("test@example.com"))
            .ThrowsAsync(exception);
        var handler = CreateHandler(userRepository.Object, GetService<ITokenRepository>(), GetService<IOAuthTokenRepository>(), GetService<IOAuthProviderClient>());
        var message = new LoginMessage(
            new Login { Email = "test@example.com", Password = "password" },
            new DefaultHttpContext());

        // Act
        var result = await handler.Handle(message);

        // Assert
        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal("Login failed.", result.Message);
        userRepository.Verify(repository => repository.GetByEmailAddressAsync("test@example.com"), Times.Once);
        userRepository.VerifyNoOtherCalls();
    }

    public static TheoryData<Exception> LoginRepositoryExceptions => new()
    {
        new Exception("Repository failed."),
        new OperationCanceledException("Repository operation was canceled.")
    };

    [Fact]
    public async Task ResetUserTokensAsync_OneStoreThrows_StillAttemptsBothStores()
    {
        // Arrange
        var tokens = new Mock<ITokenRepository>();
        var oauthTokens = new Mock<IOAuthTokenRepository>();
        tokens.Setup(repository => repository.RemoveAllByUserIdAsync("test-user", null))
            .Throws(new InvalidOperationException("Token store unavailable."));
        oauthTokens.Setup(repository => repository.RemoveAllByUserIdAsync("test-user", null))
            .ReturnsAsync(0L);
        var handler = CreateHandler(Mock.Of<IUserRepository>(), tokens.Object, oauthTokens.Object, GetService<IOAuthProviderClient>());
        var method = typeof(AuthHandler).GetMethod("ResetUserTokensAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Act
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(handler,
            [new User { Id = "test-user", EmailAddress = "user@example.test", AuthenticationVersion = "current" }, "test", new DefaultHttpContext()]));
        await task;

        // Assert
        tokens.Verify(repository => repository.RemoveAllByUserIdAsync("test-user", null), Times.Once);
        oauthTokens.Verify(repository => repository.RemoveAllByUserIdAsync("test-user", null), Times.Once);
        tokens.VerifyNoOtherCalls();
        oauthTokens.VerifyNoOtherCalls();
    }

    private AuthHandler CreateHandler(IUserRepository userRepository, ITokenRepository tokenRepository, IOAuthTokenRepository oauthTokenRepository, IOAuthProviderClient providerClient)
    {
        var appOptions = GetService<AppOptions>();

        return new AuthHandler(
            appOptions.AuthOptions,
            appOptions.IntercomOptions,
            GetService<IOrganizationRepository>(),
            userRepository,
            tokenRepository,
            oauthTokenRepository,
            providerClient,
            GetService<ICacheClient>(),
            GetService<AuthService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }
}
