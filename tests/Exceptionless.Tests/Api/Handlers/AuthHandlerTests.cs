using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class AuthHandlerTests : TestWithServices
{
    public AuthHandlerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [MemberData(nameof(LoginRepositoryExceptions))]
    public async Task Handle_LoginRepositoryException_ReturnsUnauthorizedResult(Exception exception, bool currentRead)
    {
        // Arrange
        var userRepository = new Mock<IUserRepository>();
        if (currentRead)
        {
            userRepository.Setup(repository => repository.GetByEmailAddressAsync("test@example.com"))
                .ReturnsAsync(new User { Id = TestConstants.UserId, EmailAddress = "test@example.com", Salt = "salt" });
            userRepository.Setup(repository => repository.GetByIdAsync(TestConstants.UserId, It.IsAny<ICommandOptions>()))
                .ThrowsAsync(exception);
        }
        else
        {
            userRepository.Setup(repository => repository.GetByEmailAddressAsync("test@example.com"))
                .ThrowsAsync(exception);
        }

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
        userRepository.Verify(repository => repository.GetByIdAsync(TestConstants.UserId, It.IsAny<ICommandOptions>()), Times.Exactly(currentRead ? 1 : 0));
        userRepository.VerifyNoOtherCalls();
    }

    public static TheoryData<Exception, bool> LoginRepositoryExceptions => new()
    {
        { new Exception("Repository failed."), false },
        { new OperationCanceledException("Repository operation was canceled."), false },
        { new Exception("Repository failed."), true },
        { new OperationCanceledException("Repository operation was canceled."), true }
    };

    private AuthHandler CreateHandler(IUserRepository userRepository)
    {
        var appOptions = GetService<AppOptions>();

        return new AuthHandler(
            appOptions.AuthOptions,
            appOptions.IntercomOptions,
            GetService<IOrganizationRepository>(),
            userRepository,
            GetService<ITokenRepository>(),
            GetService<IOAuthTokenRepository>(),
            GetService<IOAuthProviderClient>(),
            GetService<ICacheClient>(),
            new AuthService(userRepository, GetService<ICacheClient>(), TimeProvider, Log),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }
}
