using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Microsoft.IdentityModel.Tokens;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace Exceptionless.Web.Api.Handlers;

public class AuthHandler(
    AuthOptions authOptions,
    IntercomOptions intercomOptions,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    ITokenRepository tokenRepository,
    ICacheClient cacheClient,
    IMailer mailer,
    IDomainLoginProvider domainLoginProvider,
    TimeProvider timeProvider,
    ILogger<AuthHandler> logger)
{
    private readonly ScopedCacheClient _cache = new(cacheClient, "Auth");
    private static bool _isFirstUserChecked;
    private static readonly TimeSpan IntercomJwtLifetime = TimeSpan.FromMinutes(60);

    public async Task<Result<TokenResult>> Handle(LoginMessage message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        string email = model.Email.Trim().ToLowerInvariant();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Login").Identity(email).SetHttpContext(httpContext));

        string userLoginAttemptsCacheKey = $"user:{email}:attempts";
        long userLoginAttempts = await _cache.IncrementAsync(userLoginAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));

        string ipLoginAttemptsCacheKey = $"ip:{httpContext.Request.GetClientIpAddress()}:attempts";
        long ipLoginAttempts = await _cache.IncrementAsync(ipLoginAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));

        if (userLoginAttempts > 5)
        {
            logger.LogError("Login denied for {EmailAddress} for the {UserLoginAttempts} time", email, userLoginAttempts);
            return Result.Unauthorized("Login denied.");
        }

        if (ipLoginAttempts > 15)
        {
            logger.LogError("Login denied for {EmailAddress} for the {IPLoginAttempts} time", httpContext.Request.GetClientIpAddress(), ipLoginAttempts);
            return Result.Unauthorized("Login denied.");
        }

        User? user;
        try
        {
            user = await userRepository.GetByEmailAddressAsync(email);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Login failed for {EmailAddress}: {Message}", email, ex.Message);
            return Result.Unauthorized("Login failed.");
        }

        if (user is null)
        {
            logger.LogError("Login failed for {EmailAddress}: User not found", email);
            return Result.Unauthorized("Login failed.");
        }

        if (!user.IsActive)
        {
            logger.LogError("Login failed for {EmailAddress}: The user is inactive", user.EmailAddress);
            return Result.Unauthorized("Login failed.");
        }

        if (!authOptions.EnableActiveDirectoryAuth)
        {
            if (String.IsNullOrEmpty(user.Salt))
            {
                logger.LogError("Login failed for {EmailAddress}: The user has no salt defined", user.EmailAddress);
                return Result.Unauthorized("Login failed.");
            }

            if (!user.IsCorrectPassword(model.Password))
            {
                logger.LogError("Login failed for {EmailAddress}: Invalid Password", user.EmailAddress);
                return Result.Unauthorized("Login failed.");
            }
        }
        else if (!IsValidActiveDirectoryLogin(email, model.Password))
        {
            logger.LogError("Domain login failed for {EmailAddress}: Invalid Password or Account", user.EmailAddress);
            return Result.Unauthorized("Login failed.");
        }

        if (!String.IsNullOrEmpty(model.InviteToken))
            await AddInvitedUserToOrganizationAsync(model.InviteToken, user, httpContext);

        await _cache.RemoveAsync(userLoginAttemptsCacheKey);
        await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));

        logger.UserLoggedIn(user.EmailAddress);
        return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
    }

    public Task<Result<TokenResult>> Handle(GetIntercomToken message)
    {
        var httpContext = message.Context;

        if (!intercomOptions.EnableIntercom || String.IsNullOrWhiteSpace(intercomOptions.IntercomSecret))
            return Task.FromResult(TokenValidationProblem("intercom", "Intercom is not enabled."));

        var currentUser = httpContext.Request.GetUser();
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(IntercomJwtLifetime);

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(intercomOptions.IntercomSecret!)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            header: new JwtHeader(signingCredentials),
            payload: new JwtPayload
            {
                [JwtRegisteredClaimNames.Exp] = expiresAt.ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Iat] = issuedAt.ToUnixTimeSeconds(),
                ["user_id"] = currentUser.Id,
            }
        );

        return Task.FromResult<Result<TokenResult>>(new TokenResult { Token = new JwtSecurityTokenHandler().WriteToken(token) });
    }

    public async Task<Result> Handle(LogoutMessage message)
    {
        var httpContext = message.Context;
        var currentUser = httpContext.Request.GetUser();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Logout").Identity(currentUser.EmailAddress).SetHttpContext(httpContext));

        if (httpContext.User.IsTokenAuthType())
            return Result.Forbidden("Logout not supported for current user access token");

        string? id = httpContext.User.GetLoggedInUsersTokenId();
        if (String.IsNullOrEmpty(id))
            return Result.Forbidden("Logout not supported");

        try
        {
            await tokenRepository.RemoveAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Logout failed for {EmailAddress}: {Message}", currentUser.EmailAddress, ex.Message);
            throw;
        }

        return Result.Success();
    }

    public async Task<Result<TokenResult>> Handle(SignupMessage message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        string email = model.Email.Trim().ToLowerInvariant();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Signup").Identity(email).Property("Name", model.Name).Property("Password Length", model.Password.Length).SetHttpContext(httpContext));

        bool valid = await IsAccountCreationEnabledAsync(model.InviteToken);
        if (!valid)
            return Result.Forbidden("Account Creation is currently disabled");

        User? user;
        try
        {
            user = await userRepository.GetByEmailAddressAsync(email);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, ex.Message);
            throw;
        }

        if (user is not null)
            return await Handle(new LoginMessage(model, httpContext));

        string ipSignupAttemptsCacheKey = $"ip:{httpContext.Request.GetClientIpAddress()}:signup:attempts";
        bool hasValidInviteToken = !String.IsNullOrWhiteSpace(model.InviteToken) && await organizationRepository.GetByInviteTokenAsync(model.InviteToken) is not null;
        if (!hasValidInviteToken)
        {
            long ipSignupAttempts = await _cache.IncrementAsync(ipSignupAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
            if (ipSignupAttempts > 10)
            {
                logger.LogError("Signup denied for {EmailAddress} for the {IPSignupAttempts} time", email, ipSignupAttempts);
                return Result.Unauthorized("Signup denied.");
            }
        }

        if (authOptions.EnableActiveDirectoryAuth && !IsValidActiveDirectoryLogin(email, model.Password))
        {
            logger.LogError("Signup failed for {EmailAddress}: Active Directory authentication failed", email);
            return Result.Unauthorized("Signup failed.");
        }

        user = new User
        {
            IsActive = true,
            FullName = model.Name.Trim(),
            EmailAddress = email,
            IsEmailAddressVerified = authOptions.EnableActiveDirectoryAuth
        };

        if (user.IsEmailAddressVerified)
            user.MarkEmailAddressVerified();
        else
            user.ResetVerifyEmailAddressTokenAndExpiration(timeProvider);

        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);
        await AddGlobalAdminRoleIfFirstUserAsync(user);

        if (!authOptions.EnableActiveDirectoryAuth)
        {
            user.Salt = Core.Extensions.StringExtensions.GetRandomString(16);
            user.Password = model.Password.ToSaltedHash(user.Salt);
        }

        try
        {
            user = await userRepository.AddAsync(user, o => o.Cache());
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, ex.Message);
            throw;
        }

        if (hasValidInviteToken)
            await AddInvitedUserToOrganizationAsync(model.InviteToken, user, httpContext);

        if (!user.IsEmailAddressVerified)
            await mailer.SendUserEmailVerifyAsync(user);

        logger.UserSignedUp(user.EmailAddress);
        return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
    }

    public Task<Result<TokenResult>> Handle(GitHubLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.GitHubId,
            authOptions.GitHubSecret,
            (factory, configuration) =>
            {
                configuration.Scope = "user:email";
                return new GitHubClient(factory, configuration);
            }
        );
    }

    public Task<Result<TokenResult>> Handle(GoogleLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.GoogleId,
            authOptions.GoogleSecret,
            (factory, configuration) =>
            {
                configuration.Scope = "profile email";
                return new GoogleClient(factory, configuration);
            }
        );
    }

    public Task<Result<TokenResult>> Handle(FacebookLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.FacebookId,
            authOptions.FacebookSecret,
            (factory, configuration) =>
            {
                configuration.Scope = "email";
                return new FacebookClient(factory, configuration);
            }
        );
    }

    public Task<Result<TokenResult>> Handle(LiveLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.MicrosoftId,
            authOptions.MicrosoftSecret,
            (factory, configuration) =>
            {
                configuration.Scope = "wl.emails";
                return new WindowsLiveClient(factory, configuration);
            }
        );
    }

    public async Task<Result<TokenResult>> Handle(RemoveExternalLogin message)
    {
        var httpContext = message.Context;
        var user = httpContext.Request.GetUser();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("External Login").Tag(message.ProviderName).Identity(user.EmailAddress).Property("User", user).Property("Provider User Id", message.ProviderUserId?.Value).SetHttpContext(httpContext));

        if (String.IsNullOrWhiteSpace(message.ProviderName) || String.IsNullOrWhiteSpace(message.ProviderUserId?.Value))
        {
            logger.LogError("Remove external login failed for {EmailAddress}: Invalid Provider Name or Provider User Id", user.EmailAddress);
            return Result.BadRequest("Invalid Provider Name or Provider User Id.");
        }

        if (user.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(user.Password))
        {
            logger.LogError("Remove external login failed for {EmailAddress}: You must set a local password before removing your external login", user.EmailAddress);
            return Result.BadRequest("You must set a local password before removing your external login.");
        }

        try
        {
            if (user.RemoveOAuthAccount(message.ProviderName, message.ProviderUserId.Value))
                await userRepository.SaveAsync(user, o => o.Cache());
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error removing external login for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
            throw;
        }

        await ResetUserTokensAsync(user, "RemoveExternalLoginAsync", httpContext);

        logger.UserRemovedExternalLogin(user.EmailAddress, message.ProviderName);
        return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
    }

    public async Task<Result<TokenResult>> Handle(ChangePassword message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        var user = httpContext.Request.GetUser();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Change Password").Identity(user.EmailAddress).Property("User", user).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(httpContext));

        if (!String.IsNullOrWhiteSpace(user.Password))
        {
            if (String.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                logger.LogError("Change password failed for {EmailAddress}: The current password is incorrect", user.EmailAddress);
                return TokenValidationProblem("current_password", "The current password is incorrect.");
            }

            string encodedPassword = model.CurrentPassword.ToSaltedHash(user.Salt!);
            if (!String.Equals(encodedPassword, user.Password))
            {
                logger.LogError("Change password failed for {EmailAddress}: The current password is incorrect", user.EmailAddress);
                return TokenValidationProblem("current_password", "The current password is incorrect.");
            }

            string newPasswordHash = model.Password!.ToSaltedHash(user.Salt!);
            if (String.Equals(newPasswordHash, user.Password))
            {
                logger.LogError("Change password failed for {EmailAddress}: The new password is the same as the current password", user.EmailAddress);
                return TokenValidationProblem("password", "The new password must be different than the previous password.");
            }
        }

        await ChangePasswordAsync(user, model.Password!, nameof(ChangePasswordAsync), httpContext);
        await ResetUserTokensAsync(user, nameof(ChangePasswordAsync), httpContext);

        string userLoginAttemptsCacheKey = $"user:{user.EmailAddress}:attempts";
        await _cache.RemoveAsync(userLoginAttemptsCacheKey);

        string ipLoginAttemptsCacheKey = $"ip:{httpContext.Request.GetClientIpAddress()}:attempts";
        long attempts = await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));
        if (attempts <= 0)
            await _cache.RemoveAsync(ipLoginAttemptsCacheKey);

        logger.UserChangedPassword(user.EmailAddress);
        return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
    }

    public async Task<Result> Handle(CheckEmailAddress message)
    {
        var httpContext = message.Context;
        string email = message.Email;

        if (String.IsNullOrWhiteSpace(email))
            return Result.NoContent();

        email = email.Trim().ToLowerInvariant();
        if (httpContext.User.IsUserAuthType() && String.Equals(httpContext.Request.GetUser().EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return Result.Created();

        string ipEmailAddressAttemptsCacheKey = $"ip:{httpContext.Request.GetClientIpAddress()}:email:attempts";
        long attempts = await _cache.IncrementAsync(ipEmailAddressAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));

        if (attempts > 3 || await userRepository.GetByEmailAddressAsync(email) is null)
            return Result.NoContent();

        return Result.Created();
    }

    public async Task<Result> Handle(ForgotPassword message)
    {
        var httpContext = message.Context;
        string email = message.Email;
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Forgot Password").Identity(email).SetHttpContext(httpContext));

        if (String.IsNullOrWhiteSpace(email))
        {
            logger.LogError("Forgot password failed: Please specify a valid Email Address");
            return Result.BadRequest("Please specify a valid Email Address.");
        }

        string ipResetPasswordAttemptsCacheKey = $"ip:{httpContext.Request.GetClientIpAddress()}:password:attempts";
        long attempts = await _cache.IncrementAsync(ipResetPasswordAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        if (attempts > 3)
        {
            logger.LogError("Login denied for {EmailAddress} for the {ResetPasswordAttempts} time", email, attempts);
            return Result.Success();
        }

        email = email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAddressAsync(email);
        if (user is null)
        {
            logger.LogError("Forgot password failed for {EmailAddress}: No user was found", email);
            return Result.Success();
        }

        user.CreatePasswordResetToken(timeProvider);
        await userRepository.SaveAsync(user, o => o.Cache());

        await mailer.SendUserPasswordResetAsync(user);
        logger.UserForgotPassword(user.EmailAddress);
        return Result.Success();
    }

    public async Task<Result> Handle(ResetPassword message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        var user = await userRepository.GetByPasswordResetTokenAsync(model.PasswordResetToken);
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Reset Password").Identity(user?.EmailAddress).Property("User", user).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(httpContext));

        if (user is null)
        {
            logger.LogError("Reset password failed: Invalid Password Reset Token");
            return Result.Invalid(ValidationError.Create("password_reset_token", "Invalid Password Reset Token"));
        }

        if (!user.HasValidPasswordResetTokenExpiration(timeProvider))
        {
            logger.LogError("Reset password failed for {EmailAddress}: Password Reset Token has expired", user.EmailAddress);
            return Result.Invalid(ValidationError.Create("password_reset_token", "Password Reset Token has expired"));
        }

        if (!String.IsNullOrWhiteSpace(user.Password))
        {
            string newPasswordHash = model.Password!.ToSaltedHash(user.Salt!);
            if (String.Equals(newPasswordHash, user.Password))
            {
                logger.LogError("Reset password failed for {EmailAddress}: The new password is the same as the current password", user.EmailAddress);
                return Result.Invalid(ValidationError.Create("password", "The new password must be different than the previous password"));
            }
        }

        user.MarkEmailAddressVerified();
        await ChangePasswordAsync(user, model.Password!, "ResetPasswordAsync", httpContext);
        await ResetUserTokensAsync(user, "ResetPasswordAsync", httpContext);

        string userLoginAttemptsCacheKey = $"user:{user.EmailAddress}:attempts";
        await _cache.RemoveAsync(userLoginAttemptsCacheKey);

        string ipLoginAttemptsCacheKey = $"ip:{httpContext.Request.GetClientIpAddress()}:attempts";
        long attempts = await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));
        if (attempts <= 0)
            await _cache.RemoveAsync(ipLoginAttemptsCacheKey);

        logger.UserResetPassword(user.EmailAddress);
        return Result.Success();
    }

    public async Task<Result> Handle(CancelResetPassword message)
    {
        var httpContext = message.Context;
        string token = message.Token;

        if (String.IsNullOrEmpty(token))
        {
            using (logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").SetHttpContext(httpContext)))
                logger.LogError("Cancel reset password failed: Invalid Password Reset Token");

            return Result.BadRequest("Invalid password reset token.");
        }

        var user = await userRepository.GetByPasswordResetTokenAsync(token);
        if (user is null)
            return Result.Success();

        user.ResetPasswordResetToken();
        await userRepository.SaveAsync(user, o => o.Cache());

        using (logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").Identity(user.EmailAddress).Property("User", user).SetHttpContext(httpContext)))
            logger.UserCanceledResetPassword(user.EmailAddress);

        return Result.Success();
    }

    private async Task AddGlobalAdminRoleIfFirstUserAsync(User user)
    {
        if (_isFirstUserChecked)
            return;

        bool isFirstUser = await userRepository.CountAsync() == 0;
        if (isFirstUser)
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);

        _isFirstUserChecked = true;
    }

    private async Task<Result<TokenResult>> ExternalLoginAsync<TClient>(ExternalAuthInfo authInfo, HttpContext httpContext, string? appId, string? appSecret, Func<IRequestFactory, IClientConfiguration, TClient> createClient) where TClient : OAuth2Client
    {
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("External Login").SetHttpContext(httpContext));
        if (String.IsNullOrEmpty(appId) || String.IsNullOrEmpty(appSecret))
            throw new ConfigurationErrorsException("Missing Configuration for OAuth provider");

        var client = createClient(new RequestFactory(), new OAuth2.Configuration.ClientConfiguration
        {
            ClientId = appId,
            ClientSecret = appSecret,
            RedirectUri = authInfo.RedirectUri
        });

        UserInfo userInfo;
        try
        {
            userInfo = await client.GetUserInfoAsync(authInfo.Code, authInfo.RedirectUri);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "External login failed Code={AuthCode} RedirectUri={AuthRedirectUri}: {Message}", authInfo.Code, authInfo.RedirectUri, ex.Message);
            throw;
        }

        User? user;
        try
        {
            user = await FromExternalLoginAsync(userInfo, httpContext);
        }
        catch (ApplicationException ex)
        {
            logger.LogCritical(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
            return Result.Forbidden("Account Creation is currently disabled");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
            throw;
        }

        if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
            await AddInvitedUserToOrganizationAsync(authInfo.InviteToken, user, httpContext);

        logger.UserLoggedIn(user.EmailAddress);
        return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
    }

    private async Task<User> FromExternalLoginAsync(UserInfo userInfo, HttpContext httpContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.ProviderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.Email);

        var existingUser = await userRepository.GetUserByOAuthProviderAsync(userInfo.ProviderName, userInfo.Id);
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("External Login").Property("User Info", userInfo).Property("ExistingUser", existingUser).SetHttpContext(httpContext));

        if (httpContext.User.IsUserAuthType())
        {
            var currentUser = httpContext.Request.GetUser();
            if (existingUser is not null)
            {
                if (existingUser.Id != currentUser.Id)
                {
                    if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id))
                        throw new Exception($"Unable to remove existing oauth account for existing user: {existingUser.EmailAddress}");

                    await userRepository.SaveAsync(existingUser, o => o.Cache());
                }
                else
                {
                    return currentUser;
                }
            }

            currentUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
            return await userRepository.SaveAsync(currentUser, o => o.Cache());
        }

        if (existingUser is not null)
        {
            if (!existingUser.IsEmailAddressVerified)
            {
                existingUser.MarkEmailAddressVerified();
                await userRepository.SaveAsync(existingUser, o => o.Cache());
            }

            return existingUser;
        }

        var user = !String.IsNullOrEmpty(userInfo.Email) ? await userRepository.GetByEmailAddressAsync(userInfo.Email) : null;
        if (user is null)
        {
            if (!authOptions.EnableAccountCreation)
                throw new ApplicationException("Account Creation is currently disabled.");

            user = new User { FullName = userInfo.GetFullName()!, EmailAddress = userInfo.Email };
            user.Roles.Add(AuthorizationRoles.Client);
            user.Roles.Add(AuthorizationRoles.User);
            await AddGlobalAdminRoleIfFirstUserAsync(user);
        }

        user.MarkEmailAddressVerified();
        user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);

        if (String.IsNullOrEmpty(user.Id))
            await userRepository.AddAsync(user, o => o.Cache());
        else
            await userRepository.SaveAsync(user, o => o.Cache());

        return user;
    }

    private async Task<bool> IsAccountCreationEnabledAsync(string? token)
    {
        if (authOptions.EnableAccountCreation)
            return true;

        if (String.IsNullOrEmpty(token))
            return false;

        var organization = await organizationRepository.GetByInviteTokenAsync(token);
        return organization is not null;
    }

    private async Task AddInvitedUserToOrganizationAsync(string? token, User user, HttpContext httpContext)
    {
        if (String.IsNullOrWhiteSpace(token))
            return;

        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Invite").Identity(user.EmailAddress).Property("User", user).SetHttpContext(httpContext));
        var organization = await organizationRepository.GetByInviteTokenAsync(token);
        var invite = organization?.GetInvite(token);
        if (organization is null || invite is null)
        {
            logger.UnableToAddInvitedUserInvalidToken(user.EmailAddress, token);
            return;
        }

        if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase))
        {
            logger.MarkedInvitedUserAsVerified(user.EmailAddress);
            user.MarkEmailAddressVerified();
            await userRepository.SaveAsync(user, o => o.Cache());
        }

        if (!user.OrganizationIds.Contains(organization.Id))
        {
            logger.UserJoinedFromInvite(user.EmailAddress);
            user.OrganizationIds.Add(organization.Id);
            await userRepository.SaveAsync(user, o => o.Cache());
        }

        organization.Invites.Remove(invite);
        await organizationRepository.SaveAsync(organization, o => o.Cache());
    }

    private async Task ChangePasswordAsync(User user, string password, string tag, HttpContext httpContext)
    {
        using var _ = logger.BeginScope(new ExceptionlessState().Tag(tag).Identity(user.EmailAddress).SetHttpContext(httpContext));
        if (String.IsNullOrEmpty(user.Salt))
            user.Salt = Core.Extensions.StringExtensions.GetNewToken();

        user.Password = password.ToSaltedHash(user.Salt);
        user.ResetPasswordResetToken();

        try
        {
            await userRepository.SaveAsync(user, o => o.Cache());
            logger.ChangedUserPassword(user.EmailAddress);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error changing password for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
            throw;
        }
    }

    private async Task ResetUserTokensAsync(User user, string tag, HttpContext httpContext)
    {
        using var _ = logger.BeginScope(new ExceptionlessState().Tag(tag).Identity(user.EmailAddress).SetHttpContext(httpContext));
        try
        {
            long total = await tokenRepository.RemoveAllByUserIdAsync(user.Id);
            logger.RemovedUserTokens(total, user.EmailAddress);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error removing user tokens for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
        }
    }

    private async Task<string> GetOrCreateAuthenticationTokenAsync(User user)
    {
        var userTokens = await tokenRepository.GetByTypeAndUserIdAsync(TokenType.Authentication, user.Id);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var validAccessToken = userTokens.Documents.FirstOrDefault(token => !token.ExpiresUtc.HasValue || token.ExpiresUtc > utcNow);
        if (validAccessToken is not null)
            return validAccessToken.Id;

        var token = await tokenRepository.AddAsync(new Token
        {
            Id = Core.Extensions.StringExtensions.GetNewToken(),
            UserId = user.Id,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            ExpiresUtc = utcNow.AddMonths(3),
            CreatedBy = user.Id,
            Type = TokenType.Authentication
        }, o => o.Cache());

        return token.Id;
    }

    private bool IsValidActiveDirectoryLogin(string email, string? password)
    {
        if (String.IsNullOrEmpty(password))
            return false;

        string? domainUsername = domainLoginProvider.GetUsernameFromEmailAddress(email);
        return domainUsername is not null && domainLoginProvider.Login(domainUsername, password);
    }

    private static Result<TokenResult> TokenValidationProblem(string key, string error)
        => Result.Invalid(ValidationError.Create(key.ToLowerUnderscoredWords(), error));
}
