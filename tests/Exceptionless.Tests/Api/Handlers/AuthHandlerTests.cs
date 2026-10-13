using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
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
        var handler = CreateHandler(Mock.Of<IUserRepository>(), tokens.Object, oauthTokens.Object);
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

    private AuthHandler CreateHandler(IUserRepository userRepository, ITokenRepository? tokenRepository = null, IOAuthTokenRepository? oauthTokenRepository = null)
    {
        var appOptions = GetService<AppOptions>();

        return new AuthHandler(
            appOptions.AuthOptions,
            appOptions.IntercomOptions,
            GetService<IOrganizationRepository>(),
            userRepository,
            tokenRepository ?? GetService<ITokenRepository>(),
            oauthTokenRepository ?? GetService<IOAuthTokenRepository>(),
            GetService<IOAuthProviderClient>(),
            GetService<ICacheClient>(),
            GetService<AuthService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }
}
