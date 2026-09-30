using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using OAuth2.Models;
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
        var handler = CreateHandler(exception);
        var message = new LoginMessage(
            new Login { Email = "test@example.com", Password = "password" },
            new DefaultHttpContext());

        // Act
        var result = await handler.Handle(message);

        // Assert
        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal("Login failed.", result.Message);
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
        // Arrange
        var userRepository = DispatchProxy.Create<IUserRepository, EmailMatchUserRepositoryProxy>();
        var repository = (EmailMatchUserRepositoryProxy)(object)userRepository;
        repository.User = new User { Id = "existing-user", EmailAddress = "matching-user@exceptionless.test" };
        repository.User.AddOAuthAccount("WindowsLive", "legacy-user", repository.User.EmailAddress);
        var handler = CreateHandler(userRepository);

        // Act
        var result = await handler.Handle(new MicrosoftLogin(
            new ExternalAuthInfo { ClientId = "microsoft-client-id", Code = "matching-user", RedirectUri = "http://localhost", InviteToken = inviteToken },
            new DefaultHttpContext()));

        // Assert
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Contains("link", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("windowslive", Assert.Single(repository.User.OAuthAccounts).Provider);
        Assert.False(repository.User.IsEmailAddressVerified);
    }

    [Fact]
    public async Task Handle_MicrosoftProviderFails_DoesNotAccessUserRepository()
    {
        // Arrange
        var userRepository = DispatchProxy.Create<IUserRepository, EmailMatchUserRepositoryProxy>();
        var oauthProvider = DispatchProxy.Create<IOAuthProviderClient, FailingOAuthProviderProxy>();
        var handler = CreateHandler(userRepository, oauthProvider);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new MicrosoftLogin(
            new ExternalAuthInfo { ClientId = "microsoft-client-id", Code = "provider-failure", RedirectUri = "http://localhost" },
            new DefaultHttpContext())));

        // Assert
        Assert.Equal(0, ((EmailMatchUserRepositoryProxy)(object)userRepository).CallCount);
    }

    private AuthHandler CreateHandler(Exception repositoryException)
    {
        var userRepository = DispatchProxy.Create<IUserRepository, ThrowingUserRepositoryProxy>();
        ((ThrowingUserRepositoryProxy)(object)userRepository).Exception = repositoryException;
        return CreateHandler(userRepository);
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
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
    }

    private class EmailMatchUserRepositoryProxy : DispatchProxy
    {
        public User User { get; set; } = null!;
        public int CallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            CallCount++;
            return targetMethod?.Name switch
            {
                nameof(IUserRepository.GetUserByOAuthProviderAsync) => Task.FromResult<User?>(null),
                nameof(IUserRepository.GetByEmailAddressAsync) => Task.FromResult<User?>(User),
                _ => throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}")
            };
        }
    }

    private class FailingOAuthProviderProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IOAuthProviderClient.GetMicrosoftUserInfoAsync))
                return Task.FromException<UserInfo>(new InvalidOperationException("Microsoft provider failed."));

            throw new NotSupportedException($"Unexpected provider call: {targetMethod?.Name}");
        }
    }

    private class ThrowingUserRepositoryProxy : DispatchProxy
    {
        public Exception Exception { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IUserRepository.GetByEmailAddressAsync))
                return Task.FromException<User?>(Exception);

            throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
        }
    }
}
