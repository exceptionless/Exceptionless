using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Validation;
using Exceptionless.Tests.Utility;
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Handle_LinkExternalAccountOwnedByAnotherUser_RevokesPreviousOwnerSessions(bool cleanupFails, bool hasPreviousVersion)
    {
        // Arrange
        const string provider = "github";
        const string providerId = "123456789";
        string? previousVersion = hasPreviousVersion ? StringExtensions.GetNewToken() : null;
        string currentVersion = StringExtensions.GetNewToken();
        var displaced = GetService<UserData>().GenerateUser(id: TestConstants.UserId, emailAddress: TestConstants.UserEmail) with { AuthenticationVersion = previousVersion };
        displaced.AddOAuthAccount(provider, providerId, displaced.EmailAddress);
        displaced.AddOAuthAccount("google", "104567890123456789012", displaced.EmailAddress);
        var current = GetService<UserData>().GenerateUser(id: TestConstants.UserId2, emailAddress: TestConstants.UserEmail2) with { AuthenticationVersion = currentVersion };
        displaced.MarkEmailAddressVerified();
        current.MarkEmailAddressVerified();
        await GetService<MiniValidationValidator>().ValidateAndThrowAsync(displaced);
        await GetService<MiniValidationValidator>().ValidateAndThrowAsync(current);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(IdentityUtils.UserAuthenticationType)) };
        context.Request.SetUser(current);
        var calls = new List<string>();
        string? persistedVersion = null;
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetUserByOAuthProviderAsync(provider, providerId)).ReturnsAsync(displaced);
        users.Setup(repository => repository.SaveAsync(displaced, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback(() =>
            {
                persistedVersion = displaced.AuthenticationVersion;
                calls.Add("save-displaced");
            }).ReturnsAsync(displaced);
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
        Assert.Equal(currentVersion, current.AuthenticationVersion);
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
        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository.Setup(repository => repository.GetByEmailAddressAsync("test@example.com"))
            .ThrowsAsync(exception);
        var handler = CreateHandler(userRepository.Object, GetService<ITokenRepository>(), GetService<IOAuthTokenRepository>(), GetService<IOAuthProviderClient>());
        var message = new LoginMessage(
            new Login { Email = "test@example.com", Password = TestConstants.UserPassword },
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
    public async Task Handle_MicrosoftProviderFails_DoesNotAccessUserRepository()
    {
        // Arrange
        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        var oauthProvider = new Mock<IOAuthProviderClient>(MockBehavior.Strict);
        oauthProvider.Setup(provider => provider.GetMicrosoftUserInfoAsync(It.IsAny<ExternalAuthInfo>(), "microsoft-client-id", "microsoft-client-secret"))
            .ThrowsAsync(new InvalidOperationException("Microsoft provider failed."));
        var handler = CreateHandler(
            userRepository.Object,
            GetService<ITokenRepository>(),
            GetService<IOAuthTokenRepository>(),
            oauthProvider.Object);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await handler.Handle(new MicrosoftLogin(
                new ExternalAuthInfo { ClientId = "microsoft-client-id", Code = "provider-failure", RedirectUri = "http://localhost" },
                new DefaultHttpContext()));
        });

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        userRepository.VerifyNoOtherCalls();
    }

    private AuthHandler CreateHandler(IUserRepository userRepository, ITokenRepository tokenRepository, IOAuthTokenRepository oauthTokenRepository, IOAuthProviderClient oauthProvider)
    {
        var appOptions = GetService<AppOptions>();
        appOptions.AuthOptions.MicrosoftId = "microsoft-client-id";
        appOptions.AuthOptions.MicrosoftSecret = "microsoft-client-secret";

        return new AuthHandler(
            appOptions.AuthOptions,
            appOptions.IntercomOptions,
            GetService<IOrganizationRepository>(),
            userRepository,
            tokenRepository,
            oauthTokenRepository,
            oauthProvider,
            GetService<ICacheClient>(),
            GetService<AuthService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }
}
