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

    [Fact]
    public async Task ResetUserTokensAsync_OneStoreThrows_StillAttemptsBothStores()
    {
        var tokens = DispatchProxy.Create<ITokenRepository, CleanupRepositoryProxy>();
        var oauthTokens = DispatchProxy.Create<IOAuthTokenRepository, CleanupRepositoryProxy>();
        var tokenProxy = (CleanupRepositoryProxy)(object)tokens;
        var oauthProxy = (CleanupRepositoryProxy)(object)oauthTokens;
        tokenProxy.Throw = true;
        var handler = CreateHandler(new Exception("Unused"), tokens, oauthTokens);
        var method = typeof(AuthHandler).GetMethod("ResetUserTokensAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(handler,
            [new User { Id = "test-user", EmailAddress = "user@example.test", AuthenticationVersion = "current" }, "test", new DefaultHttpContext()]));
        await task;
        Assert.Equal(1, tokenProxy.Calls);
        Assert.Equal(1, oauthProxy.Calls);
    }

    private class CleanupRepositoryProxy : DispatchProxy
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal("RemoveAllByUserIdAsync", targetMethod!.Name);
            Calls++;
            if (Throw)
                throw new InvalidOperationException("Token store unavailable.");
            return Task.FromResult(0L);
        }
    }

    private AuthHandler CreateHandler(Exception repositoryException, ITokenRepository? tokenRepository = null, IOAuthTokenRepository? oauthTokenRepository = null)
    {
        var userRepository = DispatchProxy.Create<IUserRepository, ThrowingUserRepositoryProxy>();
        ((ThrowingUserRepositoryProxy)(object)userRepository).Exception = repositoryException;
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
            GetService<PasswordService>(),
            GetService<IMailer>(),
            GetService<IDomainLoginProvider>(),
            TimeProvider,
            Log.CreateLogger<AuthHandler>());
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
