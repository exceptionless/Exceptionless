using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Elasticsearch;
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
using Foundatio.Lock;
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ExternalAccountDestinationSaveFails_DoesNotRestoreOwnership(bool ambiguousFailure)
    {
        // Arrange
        const string provider = "github";
        const string providerId = "123456789";
        string previousGeneration = StringExtensions.GetNewToken();
        var displaced = GetService<UserData>().GenerateUser(id: TestConstants.UserId, emailAddress: TestConstants.UserEmail)
            with { AuthenticationVersion = previousGeneration, Version = new ElasticDocumentVersion(1, 1) };
        displaced.AddOAuthAccount(provider, providerId, displaced.EmailAddress);
        var destination = GetService<UserData>().GenerateUser(id: TestConstants.UserId2, emailAddress: TestConstants.UserEmail2)
            with { Version = new ElasticDocumentVersion(1, 1) };
        displaced.MarkEmailAddressVerified();
        destination.MarkEmailAddressVerified();
        await GetService<MiniValidationValidator>().ValidateAndThrowAsync(displaced);
        await GetService<MiniValidationValidator>().ValidateAndThrowAsync(destination);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(IdentityUtils.UserAuthenticationType)) };
        context.Request.SetUser(destination);
        Exception failure = ambiguousFailure
            ? new IOException("Destination save failed.")
            : new VersionConflictDocumentException("Destination user changed.");
        string? rotatedGeneration = null;
        User? currentOwner = null;
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetUserByOAuthProviderAsync(provider, providerId, It.IsAny<CancellationToken>())).ReturnsAsync(displaced);
        users.Setup(repository => repository.GetByIdAsync(displaced.Id, It.IsAny<CommandOptionsDescriptor<User>>())).ReturnsAsync(displaced);
        users.Setup(repository => repository.GetByIdAsync(destination.Id, It.IsAny<CommandOptionsDescriptor<User>>())).ReturnsAsync(destination);
        users.Setup(repository => repository.SaveAsync(It.Is<User>(user => ReferenceEquals(user, displaced)), It.IsAny<CommandOptionsDescriptor<User>>()))
            .Returns((User user, CommandOptionsDescriptor<User> _) =>
            {
                user.Version = new ElasticDocumentVersion(1, 2);
                rotatedGeneration = user.AuthenticationVersion;
                currentOwner = user with
                {
                    OAuthAccounts = [],
                    Version = new ElasticDocumentVersion(1, 2)
                };
                return Task.FromResult(user);
            });
        users.Setup(repository => repository.SaveAsync(It.Is<User>(user => ReferenceEquals(user, destination)), It.IsAny<CommandOptionsDescriptor<User>>()))
            .ThrowsAsync(failure);
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        tokens.Setup(repository => repository.RemoveAllByUserIdAsync(displaced.Id, null)).ReturnsAsync(1L);
        var oauthTokens = new Mock<IOAuthTokenRepository>(MockBehavior.Strict);
        oauthTokens.Setup(repository => repository.RemoveAllByUserIdAsync(displaced.Id, null)).ReturnsAsync(1L);
        var providerClient = new Mock<IOAuthProviderClient>(MockBehavior.Strict);
        providerClient.Setup(client => client.GetGitHubUserInfoAsync(It.IsAny<ExternalAuthInfo>(), "test-client", "test-secret"))
            .ReturnsAsync(new OAuth2.Models.UserInfo { ProviderName = provider, Id = providerId, Email = displaced.EmailAddress });
        var options = GetService<AppOptions>().AuthOptions;
        options.GitHubId = "test-client";
        options.GitHubSecret = "test-secret";
        var handler = CreateHandler(users.Object, tokens.Object, oauthTokens.Object, providerClient.Object);

        // Act
        var actual = await Record.ExceptionAsync(async () =>
        {
            _ = await handler.Handle(new GitHubLogin(new ExternalAuthInfo(), context));
        });

        // Assert
        Assert.Same(failure, actual);
        Assert.NotNull(currentOwner);
        Assert.False(String.IsNullOrEmpty(rotatedGeneration));
        Assert.NotEqual(previousGeneration, rotatedGeneration);
        Assert.Equal(rotatedGeneration, currentOwner.AuthenticationVersion);
        Assert.DoesNotContain(currentOwner.OAuthAccounts, account => String.Equals(account.Provider, provider, StringComparison.Ordinal)
            && String.Equals(account.ProviderUserId, providerId, StringComparison.Ordinal));
        users.Verify(repository => repository.GetByIdAsync(displaced.Id, It.IsAny<CommandOptionsDescriptor<User>>()), Times.Once());
        users.Verify(repository => repository.GetByIdAsync(destination.Id, It.IsAny<CommandOptionsDescriptor<User>>()), Times.Once());
        users.Verify(repository => repository.SaveAsync(It.IsAny<User>(), It.IsAny<CommandOptionsDescriptor<User>>()), Times.Exactly(2));
        users.VerifyAll();
        tokens.VerifyAll();
        oauthTokens.VerifyAll();
        providerClient.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ExternalAccountLockUnavailable_IsRejectedBeforeRepositoryAccess(bool unlink)
    {
        // Arrange
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        var oauthTokens = new Mock<IOAuthTokenRepository>(MockBehavior.Strict);
        var providerClient = new Mock<IOAuthProviderClient>(MockBehavior.Strict);
        providerClient.Setup(client => client.GetGitHubUserInfoAsync(It.IsAny<ExternalAuthInfo>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuth2.Models.UserInfo { ProviderName = "github", Id = "locked-account", Email = "locked@example.com" });
        var locks = new Mock<ILockProvider>(MockBehavior.Strict);
        locks.Setup(provider => provider.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILock?)null);
        locks.Setup(provider => provider.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LockAcquisitionTimeoutException("Account lock unavailable"));
        var options = GetService<AppOptions>().AuthOptions;
        options.GitHubId = "test-client";
        options.GitHubSecret = "test-secret";
        var handler = CreateHandler(users.Object, tokens.Object, oauthTokens.Object, providerClient.Object, locks.Object);

        var context = new DefaultHttpContext();
        var user = new User { Id = TestConstants.UserId, EmailAddress = "locked@example.com", AuthenticationVersion = "initial" };
        user.AddOAuthAccount("github", "locked-account", user.EmailAddress);
        context.Request.SetUser(user);

        // Act
        var result = unlink
            ? await handler.Handle(new RemoveExternalLogin("github", "locked-account", context))
            : await handler.Handle(new GitHubLogin(new ExternalAuthInfo(), context));

        // Assert
        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        users.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
        oauthTokens.VerifyNoOtherCalls();
        if (unlink)
        {
            providerClient.VerifyNoOtherCalls();
            Assert.Equal("initial", user.AuthenticationVersion);
            Assert.Single(user.OAuthAccounts);
        }
        else
        {
            providerClient.VerifyAll();
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    public async Task Handle_LinkExternalAccountOwnedByAnotherUser_RevokesPreviousOwnerSessions(bool cleanupFails, bool hasPreviousVersion, bool cancelAfterSave)
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
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(IdentityUtils.UserAuthenticationType)), RequestAborted = cancellation.Token };
        context.Request.SetUser(current);
        var calls = new List<string>();
        string? persistedVersion = null;
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetUsersByOAuthProviderAsync(provider, providerId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { displaced });
        users.Setup(repository => repository.GetByIdAsync(displaced.Id, It.IsAny<CommandOptionsDescriptor<User>>())).ReturnsAsync(displaced);
        users.Setup(repository => repository.GetByIdAsync(current.Id, It.IsAny<CommandOptionsDescriptor<User>>())).ReturnsAsync(current);
        users.Setup(repository => repository.SaveAsync(displaced, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback(() =>
            {
                persistedVersion = displaced.AuthenticationVersion;
                calls.Add("save-displaced");
            }).ReturnsAsync(displaced);
        users.Setup(repository => repository.SaveAsync(current, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback(() =>
            {
                calls.Add("save-current");
                if (cancelAfterSave)
                    cancellation.Cancel();
            }).ReturnsAsync(current);
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

    [Fact]
    public async Task Handle_UnlinkCanceledAfterPersistence_CompletesTokenIssuance()
    {
        // Arrange
        const string providerId = "unlink-after-persistence";
        string previousVersion = StringExtensions.GetNewToken();
        var user = GetService<UserData>().GenerateUser(id: TestConstants.UserId, emailAddress: TestConstants.UserEmail) with
        {
            Password = "local-password", AuthenticationVersion = previousVersion
        };
        user.AddOAuthAccount("github", providerId, user.EmailAddress);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Request.SetUser(user);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CommandOptionsDescriptor<User>>())).ReturnsAsync(user);
        users.Setup(repository => repository.SaveAsync(user, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Callback(() => cancellation.Cancel()).ReturnsAsync(user);
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        tokens.Setup(repository => repository.RemoveAllByUserIdAsync(user.Id, null)).ReturnsAsync(1L);
        tokens.Setup(repository => repository.GetByTypeAndUserIdAsync(TokenType.Authentication, user.Id, null)).ReturnsAsync(FindResults<Token>.Empty);
        Token? issuedToken = null;
        tokens.Setup(repository => repository.AddAsync(It.IsAny<Token>(), It.IsAny<CommandOptionsDescriptor<Token>>()))
            .Callback((Token token, CommandOptionsDescriptor<Token> _) => issuedToken = token)
            .ReturnsAsync((Token token, CommandOptionsDescriptor<Token> _) => token);
        var oauthTokens = new Mock<IOAuthTokenRepository>(MockBehavior.Strict);
        oauthTokens.Setup(repository => repository.RemoveAllByUserIdAsync(user.Id, null)).ReturnsAsync(1L);
        var handler = CreateHandler(users.Object, tokens.Object, oauthTokens.Object, Mock.Of<IOAuthProviderClient>());

        // Act
        var result = await handler.Handle(new RemoveExternalLogin("github", providerId, context));

        // Assert
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Empty(user.OAuthAccounts);
        Assert.NotEqual(previousVersion, user.AuthenticationVersion);
        Assert.NotNull(issuedToken);
        Assert.Equal(user.AuthenticationVersion, issuedToken.AuthenticationVersion);
        users.VerifyAll();
        tokens.VerifyAll();
        oauthTokens.VerifyAll();
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

    private AuthHandler CreateHandler(IUserRepository userRepository, ITokenRepository tokenRepository, IOAuthTokenRepository oauthTokenRepository, IOAuthProviderClient providerClient, ILockProvider? lockProvider = null)
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
            lockProvider ?? GetService<ILockProvider>(),
            GetService<AuthService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }
}
