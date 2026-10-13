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
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class AuthHandlerTests : TestWithServices
{
    public AuthHandlerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [MemberData(nameof(LoginRepositoryExceptions))]
    public async Task Handle_LoginRepositoryException_ReturnsUnauthorizedResult(Exception exception)
    {
        // Arrange
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAddressAsync("test@example.com"))
            .ThrowsAsync(exception);
        var handler = CreateHandler(userRepository.Object);
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

    [Theory]
    [InlineData(null)]
    [InlineData("invitation-token")]
    public async Task Handle_MicrosoftEmailMatchesUnlinkedUser_DoesNotModifyAccount(string? inviteToken)
    {
        var user = new User { Id = "existing-user", EmailAddress = "matching-user@exceptionless.test" };
        user.AddOAuthAccount("WindowsLive", "legacy-user", user.EmailAddress);
        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository.Setup(repository => repository.GetUserByOAuthProviderAsync("Microsoft", "matching-user"))
            .ReturnsAsync((User?)null);
        userRepository.Setup(repository => repository.GetByEmailAddressAsync(user.EmailAddress)).ReturnsAsync(user);
        var handler = CreateHandler(userRepository.Object);

        var result = await handler.Handle(new MicrosoftLogin(
            new ExternalAuthInfo { ClientId = "microsoft-client-id", Code = "matching-user", RedirectUri = "http://localhost", InviteToken = inviteToken },
            new DefaultHttpContext()));

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Contains("link", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("windowslive", Assert.Single(user.OAuthAccounts).Provider);
        Assert.False(user.IsEmailAddressVerified);
        userRepository.VerifyAll();
        userRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_MicrosoftProviderFails_DoesNotAccessUserRepository()
    {
        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        var oauthProvider = new Mock<IOAuthProviderClient>(MockBehavior.Strict);
        oauthProvider.Setup(provider => provider.GetMicrosoftUserInfoAsync(It.IsAny<ExternalAuthInfo>(), "microsoft-client-id", "microsoft-client-secret"))
            .ThrowsAsync(new InvalidOperationException("Microsoft provider failed."));
        var handler = CreateHandler(userRepository.Object, oauthProvider.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new MicrosoftLogin(
            new ExternalAuthInfo { ClientId = "microsoft-client-id", Code = "provider-failure", RedirectUri = "http://localhost" },
            new DefaultHttpContext())));

        userRepository.VerifyNoOtherCalls();
    }

    private AuthHandler CreateHandler(IUserRepository userRepository, IOAuthProviderClient? oauthProvider = null)
    {
        var appOptions = GetService<AppOptions>();
        appOptions.AuthOptions.MicrosoftId = "microsoft-client-id";
        appOptions.AuthOptions.MicrosoftSecret = "microsoft-client-secret";

        return new AuthHandler(
            appOptions.AuthOptions,
            appOptions.IntercomOptions,
            GetService<IOrganizationRepository>(),
            userRepository,
            GetService<ITokenRepository>(),
            GetService<IOAuthTokenRepository>(),
            oauthProvider ?? GetService<IOAuthProviderClient>(),
            GetService<ICacheClient>(),
            GetService<AuthService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }
}
