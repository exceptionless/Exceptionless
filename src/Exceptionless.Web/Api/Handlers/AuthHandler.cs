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
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Security;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Microsoft.IdentityModel.Tokens;
using OAuth2.Models;

namespace Exceptionless.Web.Api.Handlers;

public class AuthHandler(
    AuthOptions authOptions,
    IntercomOptions intercomOptions,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    ITokenRepository tokenRepository,
    IOAuthTokenRepository oauthTokenRepository,
    IOAuthProviderClient oauthProviderClient,
    ICacheClient cacheClient,
    ILockProvider lockProvider,
    AuthService authService,
    IMailer mailer,
    IDomainLoginProvider domainLoginProvider,
    TimeProvider timeProvider,
    ILogger<AuthHandler> logger)
{
    private readonly ScopedCacheClient _cache = new(cacheClient, "Auth");
    private static bool _isFirstUserChecked;
    private static readonly TimeSpan IntercomJwtLifetime = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan ExternalAccountAdmissionTimeout = TimeSpan.FromSeconds(15);

    public async Task<Result<TokenResult>> Handle(LoginMessage message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        string email = model.Email.Trim().ToLowerInvariant();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Login").Identity(email).SetHttpContext(httpContext));

        string? ipAddress = httpContext.Request.GetClientIpAddress();
        await using var loginAttempt = await authService.TryBeginLoginAsync(email, ipAddress, httpContext.RequestAborted);
        if (loginAttempt is null)
        {
            logger.LogError("Login denied for {EmailAddress}", email);
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
            await authService.RecordLoginFailureAsync(loginAttempt);
            logger.LogError("Login failed for {EmailAddress}: User not found", email);
            return Result.Unauthorized("Login failed.");
        }

        if (!user.IsActive)
        {
            await authService.RecordLoginFailureAsync(loginAttempt);
            logger.LogError("Login failed for {EmailAddress}: The user is inactive", user.EmailAddress);
            return Result.Unauthorized("Login failed.");
        }

        if (!authOptions.EnableActiveDirectoryAuth)
        {
            if (String.IsNullOrEmpty(user.Salt))
            {
                await authService.RecordLoginFailureAsync(loginAttempt);
                logger.LogError("Login failed for {EmailAddress}: The user has no salt defined", user.EmailAddress);
                return Result.Unauthorized("Login failed.");
            }

            if (!user.IsCorrectPassword(model.Password))
            {
                await authService.RecordLoginFailureAsync(loginAttempt);
                logger.LogError("Login failed for {EmailAddress}: Invalid Password", user.EmailAddress);
                return Result.Unauthorized("Login failed.");
            }
        }
        else if (!IsValidActiveDirectoryLogin(email, model.Password))
        {
            await authService.RecordLoginFailureAsync(loginAttempt);
            logger.LogError("Domain login failed for {EmailAddress}: Invalid Password or Account", user.EmailAddress);
            return Result.Unauthorized("Login failed.");
        }

        if (!String.IsNullOrEmpty(model.InviteToken))
            await AddInvitedUserToOrganizationAsync(model.InviteToken, user, httpContext);

        await authService.RecordLoginSuccessAsync(loginAttempt);

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
            oauthProviderClient.GetGitHubUserInfoAsync
        );
    }

    public Task<Result<TokenResult>> Handle(GoogleLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.GoogleId,
            authOptions.GoogleSecret,
            oauthProviderClient.GetGoogleUserInfoAsync
        );
    }

    public Task<Result<TokenResult>> Handle(FacebookLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.FacebookId,
            authOptions.FacebookSecret,
            oauthProviderClient.GetFacebookUserInfoAsync
        );
    }

    public Task<Result<TokenResult>> Handle(LiveLogin message)
    {
        return ExternalLoginAsync(message.AuthInfo, message.Context,
            authOptions.MicrosoftId,
            authOptions.MicrosoftSecret,
            oauthProviderClient.GetMicrosoftUserInfoAsync
        );
    }

    public async Task<Result<TokenResult>> Handle(RemoveExternalLogin message)
    {
        if (String.IsNullOrWhiteSpace(message.ProviderUserId))
            return Result.BadRequest("Invalid Provider Name or Provider User Id.");

        var httpContext = message.Context;
        var user = httpContext.Request.GetUser();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("External Login").Tag(message.ProviderName).Identity(user.EmailAddress).Property("Provider User Id", message.ProviderUserId).SetHttpContext(httpContext));

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        operationCancellation.CancelAfter(ExternalAccountAdmissionTimeout);
        try
        {
            operationCancellation.Token.ThrowIfCancellationRequested();
            await using var userLock = await lockProvider.AcquireAsync(GetExternalAccountUserLockKey(user.Id), cancellationToken: operationCancellation.Token);
            var storedUser = await userRepository.GetByIdAsync(user.Id, options => options.Cache(false)).WaitAsync(operationCancellation.Token);
            if (storedUser is null || !storedUser.IsActive
                || !String.Equals(user.AuthenticationVersion, storedUser.AuthenticationVersion, StringComparison.Ordinal))
                return Result.Unauthorized("Login failed.");

            user = storedUser;
            CompleteExternalAccountAdmission(operationCancellation);
            if (!user.RemoveOAuthAccount(message.ProviderName, message.ProviderUserId))
            {
                logger.LogError("Remove external login failed for {EmailAddress}: You must set a local password before removing your external login", user.EmailAddress);
                return Result.BadRequest("You must set a local password before removing your external login.");
            }

            user.AuthenticationVersion = Core.Extensions.StringExtensions.GetNewToken();
            await userRepository.SaveAsync(user, o => o.Cache());

            await ResetUserTokensAsync(user, "RemoveExternalLoginAsync", httpContext);

            logger.UserRemovedExternalLogin(user.EmailAddress, message.ProviderName);
            return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
        }
        catch (Exception ex) when (ex is LockAcquisitionTimeoutException
            || ex is OperationCanceledException && operationCancellation.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Remove external login failed for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
            return Result.Unauthorized("Login failed.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error removing external login for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
            throw;
        }
    }

    public async Task<Result<TokenResult>> Handle(ChangePassword message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        var user = httpContext.Request.GetUser();
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Change Password").Identity(user.EmailAddress).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(httpContext));

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

        await authService.ClearUserLoginAttemptsAsync(user.EmailAddress);

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
        try
        {
            await userRepository.SaveAsync(user, o => o.Cache());
        }
        catch (VersionConflictDocumentException)
        {
            return Result.Success();
        }

        await mailer.SendUserPasswordResetAsync(user);
        logger.UserForgotPassword(user.EmailAddress);
        return Result.Success();
    }

    public async Task<Result> Handle(ResetPassword message)
    {
        var httpContext = message.Context;
        var model = message.Model;
        var user = await userRepository.GetByPasswordResetTokenAsync(model.PasswordResetToken);
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Reset Password").Identity(user?.EmailAddress).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(httpContext));

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

        await authService.ClearUserLoginAttemptsAsync(user.EmailAddress);

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

        using (logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").Identity(user.EmailAddress).SetHttpContext(httpContext)))
            logger.UserCanceledResetPassword(user.EmailAddress);

        return Result.Success();
    }

    private async Task AddGlobalAdminRoleIfFirstUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (_isFirstUserChecked)
            return;

        bool isFirstUser = await userRepository.CountAsync().WaitAsync(cancellationToken) == 0;
        if (isFirstUser)
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);

        _isFirstUserChecked = true;
    }

    private async Task<Result<TokenResult>> ExternalLoginAsync(ExternalAuthInfo authInfo, HttpContext httpContext, string? appId, string? appSecret, Func<ExternalAuthInfo, string, string, Task<UserInfo>> getUserInfoAsync)
    {
        using var _ = logger.BeginScope(new ExceptionlessState().Tag("External Login").SetHttpContext(httpContext));

        if (String.IsNullOrEmpty(appId) || String.IsNullOrEmpty(appSecret))
            throw new ConfigurationErrorsException("Missing Configuration for OAuth provider");

        UserInfo userInfo;
        try
        {
            userInfo = await getUserInfoAsync(authInfo, appId, appSecret);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "External login failed RedirectUri={AuthRedirectUri}: {Message}", authInfo.RedirectUri, ex.Message);
            throw;
        }

        // Bound admission reads only. Once admitted, complete account changes and token issuance inside the locks.
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        operationCancellation.CancelAfter(ExternalAccountAdmissionTimeout);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.ProviderName);
            ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.Email);

            // Lock the identity and normalized email before resolving or creating its owner.
            string[] accountKeys = [
                $"external-account:identity:{userInfo.ProviderName}:{OAuthService.CreateTokenHash(userInfo.Id)}",
                $"external-account:email:{OAuthService.CreateTokenHash(userInfo.Email.Trim().ToLowerInvariant())}"
            ];
            operationCancellation.Token.ThrowIfCancellationRequested();
            await using var accountLock = await lockProvider.AcquireAsync(accountKeys, cancellationToken: operationCancellation.Token);
            var owners = await userRepository.GetUsersByOAuthProviderAsync(userInfo.ProviderName, userInfo.Id, operationCancellation.Token)
                .WaitAsync(operationCancellation.Token);
            if (owners.Count > 1)
            {
                logger.LogWarning("External login failed for {EmailAddress}: The external account is linked to multiple users", userInfo.Email);
                return Result.Unauthorized("Login failed.");
            }

            var existingUser = owners.SingleOrDefault();
            // Admission reads may outlive cancellation; do not read or populate a stale email cache entry.
            var emailUser = existingUser is null
                ? (await userRepository.FindOneAsync(query => query.FieldEquals(user => user.EmailAddress, userInfo.Email.Trim().ToLowerInvariant()),
                    options => options.Cache(false)).WaitAsync(operationCancellation.Token))?.Document
                : null;
            string? currentUserId = String.IsNullOrWhiteSpace(authInfo.InviteToken) && httpContext.User.IsUserAuthType()
                ? httpContext.Request.GetUser().Id : null;
            var userKeys = new[] { existingUser?.Id, emailUser?.Id, currentUserId }
                .OfType<string>().Select(GetExternalAccountUserLockKey);
            // Ordered user locks also protect transfers and different identities on the same user.
            await using var userLock = await lockProvider.AcquireAsync(userKeys, cancellationToken: operationCancellation.Token);
            operationCancellation.Token.ThrowIfCancellationRequested();
            var user = await FromExternalLoginAsync(userInfo, authInfo.InviteToken, httpContext, existingUser, emailUser, operationCancellation);

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                await AddInvitedUserToOrganizationAsync(authInfo.InviteToken, user, httpContext);

            logger.UserLoggedIn(user.EmailAddress);
            return new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or LockAcquisitionTimeoutException
            || ex is OperationCanceledException && operationCancellation.IsCancellationRequested)
        {
            logger.LogWarning(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
            return Result.Unauthorized("Login failed.");
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
    }

    private static string GetExternalAccountUserLockKey(string userId) => $"external-account:user:{userId}";

    private static void CompleteExternalAccountAdmission(CancellationTokenSource admissionCancellation)
    {
        admissionCancellation.Token.ThrowIfCancellationRequested();
        admissionCancellation.CancelAfter(Timeout.InfiniteTimeSpan);
    }

    private async Task<User> FromExternalLoginAsync(UserInfo userInfo, string? inviteToken, HttpContext httpContext, User? existingUser, User? emailUser, CancellationTokenSource admissionCancellation)
    {
        var cancellationToken = admissionCancellation.Token;
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.ProviderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userInfo.Email);

        var accountData = new SettingsDictionary { ["email"] = userInfo.Email };
        string? fullName = userInfo.GetFullName();
        if (!String.IsNullOrWhiteSpace(fullName))
            accountData["name"] = fullName;

        using var _ = logger.BeginScope(new ExceptionlessState().Tag("External Login").Tag(userInfo.ProviderName).Identity(userInfo.Email).SetHttpContext(httpContext));

        if (existingUser is not null)
        {
            existingUser = await userRepository.GetByIdAsync(existingUser.Id, options => options.Cache(false)).WaitAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException("The user no longer exists.");
            EnsureUserIsActive(existingUser);
            if (!existingUser.OAuthAccounts.Any(account =>
                String.Equals(account.Provider, userInfo.ProviderName, StringComparison.Ordinal) &&
                String.Equals(account.ProviderUserId, userInfo.Id, StringComparison.Ordinal)))
                throw new UnauthorizedAccessException("The external account is no longer linked to this user.");
        }

        if (String.IsNullOrWhiteSpace(inviteToken) && httpContext.User.IsUserAuthType())
        {
            var authenticatedUser = httpContext.Request.GetUser();
            var currentUser = await userRepository.GetByIdAsync(authenticatedUser.Id, options => options.Cache(false)).WaitAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException("The user no longer exists.");
            EnsureUserIsActive(currentUser);
            if (!String.Equals(authenticatedUser.AuthenticationVersion, currentUser.AuthenticationVersion, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("The user session has been revoked.");
            CompleteExternalAccountAdmission(admissionCancellation);
            if (existingUser is not null)
            {
                if (!String.Equals(existingUser.Id, currentUser.Id, StringComparison.Ordinal))
                {
                    if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id))
                        throw new Exception($"Unable to remove existing oauth account for existing user: {existingUser.EmailAddress}");

                    existingUser.AuthenticationVersion = Core.Extensions.StringExtensions.GetNewToken();
                    await userRepository.SaveAsync(existingUser, o => o.Cache());
                    await ResetUserTokensAsync(existingUser, "Link external account to another user", httpContext);
                }
            }

            currentUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email, accountData);
            return await userRepository.SaveAsync(currentUser, o => o.Cache());
        }

        if (existingUser is not null)
        {
            CompleteExternalAccountAdmission(admissionCancellation);
            existingUser.MarkEmailAddressVerified();
            existingUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email, accountData);
            return await userRepository.SaveAsync(existingUser, o => o.Cache());
        }

        var user = emailUser is not null ? await userRepository.GetByIdAsync(emailUser.Id, options => options.Cache(false)).WaitAsync(cancellationToken) : null;
        if (user is null)
        {
            if (!await IsAccountCreationEnabledAsync(inviteToken, cancellationToken))
                throw new ApplicationException("Account Creation is currently disabled.");

            user = new User { FullName = userInfo.GetFullName()!, EmailAddress = userInfo.Email };
            user.Roles.Add(AuthorizationRoles.Client);
            user.Roles.Add(AuthorizationRoles.User);
            await AddGlobalAdminRoleIfFirstUserAsync(user, cancellationToken);
        }

        EnsureUserIsActive(user);
        CompleteExternalAccountAdmission(admissionCancellation);
        user.MarkEmailAddressVerified();
        user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email, accountData);

        if (String.IsNullOrEmpty(user.Id))
            await userRepository.AddAsync(user, o => o.Cache());
        else
            await userRepository.SaveAsync(user, o => o.Cache());

        return user;
    }

    private static void EnsureUserIsActive(User user)
    {
        if (!user.IsActive)
            throw new UnauthorizedAccessException("The user is inactive.");
    }

    private async Task<bool> IsAccountCreationEnabledAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (authOptions.EnableAccountCreation)
            return true;

        if (String.IsNullOrEmpty(token))
            return false;

        var organization = await organizationRepository.GetByInviteTokenAsync(token).WaitAsync(cancellationToken);
        return organization is not null;
    }

    private async Task AddInvitedUserToOrganizationAsync(string? token, User user, HttpContext httpContext)
    {
        if (String.IsNullOrWhiteSpace(token))
            return;

        using var _ = logger.BeginScope(new ExceptionlessState().Tag("Invite").Identity(user.EmailAddress).SetHttpContext(httpContext));

        var organization = await organizationRepository.GetByInviteTokenAsync(token);
        var invite = organization?.GetInvite(token);
        if (organization is null || invite is null)
        {
            logger.UnableToAddInvitedUserInvalidToken(user.EmailAddress);
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
        user.AuthenticationVersion = Core.Extensions.StringExtensions.GetNewToken();
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

        long total = 0;
        try
        {
            total += await tokenRepository.RemoveAllByUserIdAsync(user.Id);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error removing user tokens for {EmailAddress} ({UserId}): {Message}", user.EmailAddress, user.Id, ex.Message);
        }

        try
        {
            total += await oauthTokenRepository.RemoveAllByUserIdAsync(user.Id);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error removing OAuth tokens for {EmailAddress} ({UserId}): {Message}", user.EmailAddress, user.Id, ex.Message);
        }

        logger.RemovedUserTokens(total, user.EmailAddress);
    }

    private async Task<string> GetOrCreateAuthenticationTokenAsync(User user)
    {
        var userTokens = await tokenRepository.GetByTypeAndUserIdAsync(TokenType.Authentication, user.Id);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var validAccessToken = userTokens.Documents.FirstOrDefault(token => !token.IsDisabled && !token.IsSuspended
            && user.HasValidAuthenticationVersion(token.AuthenticationVersion)
            && (!token.ExpiresUtc.HasValue || token.ExpiresUtc > utcNow));
        if (validAccessToken is not null)
            return validAccessToken.Id;

        var token = await tokenRepository.AddAsync(new Token
        {
            Id = Core.Extensions.StringExtensions.GetNewToken(),
            UserId = user.Id,
            AuthenticationVersion = user.AuthenticationVersion,
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
