using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Core.Validation;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
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
        using var userRepository = new ThrowingUserRepository(
            GetService<ExceptionlessElasticConfiguration>(), GetService<MiniValidationValidator>(), GetService<AppOptions>(), exception);
        var handler = CreateHandler(userRepository);
        var message = new LoginMessage(
            new Login { Email = "test@example.com", Password = "password" },
            new DefaultHttpContext());

        // Act
        var result = await handler.Handle(message);

        // Assert
        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal("Login failed.", result.Message);
        Assert.Equal("test@example.com", userRepository.RequestedEmailAddress);
        Assert.Equal(1, userRepository.GetByEmailAddressCallCount);
    }

    public static TheoryData<Exception> LoginRepositoryExceptions => new()
    {
        new Exception("Repository failed."),
        new OperationCanceledException("Repository operation was canceled.")
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
            GetService<AuthService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }

    private sealed class ThrowingUserRepository(
        ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options, Exception exception)
        : UserRepository(configuration, validator, options), IUserRepository
    {
        public string? RequestedEmailAddress { get; private set; }
        public int GetByEmailAddressCallCount { get; private set; }

        Task<User?> IUserRepository.GetByEmailAddressAsync(string emailAddress)
        {
            RequestedEmailAddress = emailAddress;
            GetByEmailAddressCallCount++;
            return Task.FromException<User?>(exception);
        }
    }
}
