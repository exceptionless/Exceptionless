using System.Configuration;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/auth")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class AuthController : ExceptionlessApiController
{
    private readonly AuthOptions _authOptions;
    private readonly IDomainLoginProvider _domainLoginProvider;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly ScopedCacheClient _cache;
    private readonly IMailer _mailer;
    private readonly ILogger _logger;

    private static bool _isFirstUserChecked;

    public AuthController(AuthOptions authOptions, IOrganizationRepository organizationRepository, IUserRepository userRepository,
        ITokenRepository tokenRepository, ICacheClient cacheClient, IMailer mailer, IDomainLoginProvider domainLoginProvider,
        TimeProvider timeProvider, ILogger<AuthController> logger) : base(timeProvider)
    {
        _authOptions = authOptions;
        _domainLoginProvider = domainLoginProvider;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _cache = new ScopedCacheClient(cacheClient, "Auth");
        _mailer = mailer;
        _logger = logger;
    }

    /// <summary>
    /// Login
    /// </summary>
    /// <remarks>
    /// Log in with your email address and password to generate a token scoped with your users roles.
    ///
    /// <code>{ "email": "noreply@exceptionless.io", "password": "exceptionless" }</code>
    ///
    /// This token can then be used to access the api. You can use this token in the header (bearer authentication)
    /// or append it onto the query string: ?access_token=MY_TOKEN
    ///
    /// Please note that you can also use this token on the documentation site by placing it in the
    /// headers api_key input box.
    /// </remarks>
    /// <response code="200">User Authentication Token</response>
    /// <response code="401">Login failed</response>
    /// <response code="422">Validation error</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResult>> LoginAsync(Login model)
    {
        string email = model.Email.Trim().ToLowerInvariant();
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Login").Identity(email).SetHttpContext(HttpContext));

        // Only allow 5 password attempts per 15-minute period.
        string userLoginAttemptsCacheKey = $"user:{email}:attempts";
        long userLoginAttempts = await _cache.IncrementAsync(userLoginAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));

        // Only allow 15 login attempts per 15-minute period by a single ip.
        string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
        long ipLoginAttempts = await _cache.IncrementAsync(ipLoginAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));

        if (userLoginAttempts > 5)
        {
            _logger.LogError("Login denied for {EmailAddress} for the {UserLoginAttempts} time", email, userLoginAttempts);
            return Unauthorized();
        }

        if (ipLoginAttempts > 15)
        {
            _logger.LogError("Login denied for {EmailAddress} for the {IPLoginAttempts} time", Request.GetClientIpAddress(), ipLoginAttempts);
            return Unauthorized();
        }

        User? user;
        try
        {
            user = await _userRepository.GetByEmailAddressAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Login failed for {EmailAddress}: {Message}", email, ex.Message);
            return Unauthorized();
        }

        if (user is null)
        {
            _logger.LogError("Login failed for {EmailAddress}: User not found", email);
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            _logger.LogError("Login failed for {EmailAddress}: The user is inactive", user.EmailAddress);
            return Unauthorized();
        }

        if (!_authOptions.EnableActiveDirectoryAuth)
        {
            if (String.IsNullOrEmpty(user.Salt))
            {
                _logger.LogError("Login failed for {EmailAddress}: The user has no salt defined", user.EmailAddress);
                return Unauthorized();
            }

            if (!user.IsCorrectPassword(model.Password))
            {
                _logger.LogError("Login failed for {EmailAddress}: Invalid Password", user.EmailAddress);
                return Unauthorized();
            }
        }
        else
        {
            if (!IsValidActiveDirectoryLogin(email, model.Password))
            {
                _logger.LogError("Domain login failed for {EmailAddress}: Invalid Password or Account", user.EmailAddress);
                return Unauthorized();
            }
        }

        if (!String.IsNullOrEmpty(model.InviteToken))
            await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

        await _cache.RemoveAsync(userLoginAttemptsCacheKey);
        await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));

        _logger.UserLoggedIn(user.EmailAddress);
        return Ok(new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) });
    }

    /// <summary>
    /// Logout the current user and remove the current access token
    /// </summary>
    /// <response code="200">User successfully logged-out</response>
    /// <response code="401">User not logged in</response>
    /// <response code="403">Current action is not supported with user access token</response>
    [HttpGet("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Logout").Identity(CurrentUser.EmailAddress).SetHttpContext(HttpContext));
        if (User.IsTokenAuthType())
            return Forbidden("Logout not supported for current user access token");

        string? id = User.GetLoggedInUsersTokenId();
        if (String.IsNullOrEmpty(id))
            return Forbidden("Logout not supported");

        try
        {
            await _tokenRepository.RemoveAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Logout failed for {EmailAddress}: {Message}", CurrentUser.EmailAddress, ex.Message);
            throw;
        }

        return Ok();
    }

    /// <summary>
    /// Sign up
    /// </summary>
    /// <response code="200">User Authentication Token</response>
    /// <response code="401">Sign-up failed</response>
    /// <response code="403">Account Creation is currently disabled</response>
    /// <response code="422">Validation error</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("signup")]
    public async Task<ActionResult<TokenResult>> SignupAsync(Signup model)
    {
        string email = model.Email.Trim().ToLowerInvariant();
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Signup").Identity(email).Property("Name", model.Name).Property("Password Length", model.Password.Length).SetHttpContext(HttpContext));

        bool valid = await IsAccountCreationEnabledAsync(model.InviteToken);
        if (!valid)
            return Forbidden("Account Creation is currently disabled");

        User? user;
        try
        {
            user = await _userRepository.GetByEmailAddressAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, ex.Message);
            throw;
        }

        if (user is not null)
            return await LoginAsync(model);

        string ipSignupAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:signup:attempts";
        bool hasValidInviteToken = !String.IsNullOrWhiteSpace(model.InviteToken) && await _organizationRepository.GetByInviteTokenAsync(model.InviteToken) is not null;
        if (!hasValidInviteToken)
        {
            // Only allow 10 sign-ups per hour period by a single ip.
            long ipSignupAttempts = await _cache.IncrementAsync(ipSignupAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
            if (ipSignupAttempts > 10)
            {
                _logger.LogError("Signup denied for {EmailAddress} for the {IPSignupAttempts} time", email, ipSignupAttempts);
                return Unauthorized();
            }
        }

        if (_authOptions.EnableActiveDirectoryAuth && !IsValidActiveDirectoryLogin(email, model.Password))
        {
            _logger.LogError("Signup failed for {EmailAddress}: Active Directory authentication failed", email);
            return Unauthorized();
        }

        user = new User
        {
            IsActive = true,
            FullName = model.Name.Trim(),
            EmailAddress = email,
            IsEmailAddressVerified = _authOptions.EnableActiveDirectoryAuth
        };

        if (user.IsEmailAddressVerified)
            user.MarkEmailAddressVerified();
        else
            user.ResetVerifyEmailAddressTokenAndExpiration(_timeProvider);

        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);
        await AddGlobalAdminRoleIfFirstUserAsync(user);

        if (!_authOptions.EnableActiveDirectoryAuth)
        {
            user.Salt = Core.Extensions.StringExtensions.GetRandomString(16);
            user.Password = model.Password!.ToSaltedHash(user.Salt);
        }

        try
        {
            user = await _userRepository.AddAsync(user, o => o.Cache());
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, ex.Message);
            throw;
        }

        if (hasValidInviteToken)
            await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

        if (!user.IsEmailAddressVerified)
            await _mailer.SendUserEmailVerifyAsync(user);

        _logger.UserSignedUp(user.EmailAddress);
        return Ok(new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) });
    }

    /// <summary>
    /// Sign in with GitHub
    /// </summary>
    /// <response code="200">User Authentication Token</response>
    /// <response code="403">Account Creation is currently disabled</response>
    /// <response code="422">Validation error</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("github")]
    public Task<ActionResult<TokenResult>> GitHubAsync(ExternalAuthInfo value)
    {
        return ExternalLoginAsync(value,
            _authOptions.GitHubId,
            _authOptions.GitHubSecret,
            (f, c) =>
            {
                c.Scope = "user:email";
                return new GitHubClient(f, c);
            }
        );
    }

    /// <summary>
    /// Sign in with Google
    /// </summary>
    /// <response code="200">User Authentication Token</response>
    /// <response code="403">Account Creation is currently disabled</response>
    /// <response code="422">Validation error</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("google")]
    public Task<ActionResult<TokenResult>> GoogleAsync(ExternalAuthInfo value)
    {
        return ExternalLoginAsync(value,
            _authOptions.GoogleId,
            _authOptions.GoogleSecret,
            (f, c) =>
            {
                c.Scope = "profile email";
                return new GoogleClient(f, c);
            }
        );
    }

    /// <summary>
    /// Sign in with Facebook
    /// </summary>
    /// <response code="200">User Authentication Token</response>
    /// <response code="403">Account Creation is currently disabled</response>
    /// <response code="422">Validation error</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("facebook")]
    public Task<ActionResult<TokenResult>> FacebookAsync(ExternalAuthInfo value)
    {
        return ExternalLoginAsync(value,
            _authOptions.FacebookId,
            _authOptions.FacebookSecret,
            (f, c) =>
            {
                c.Scope = "email";
                return new FacebookClient(f, c);
            }
        );
    }

    /// <summary>
    /// Sign in with Microsoft
    /// </summary>
    /// <response code="200">User Authentication Token</response>
    /// <response code="403">Account Creation is currently disabled</response>
    /// <response code="422">Validation error</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("live")]
    public Task<ActionResult<TokenResult>> LiveAsync(ExternalAuthInfo value)
    {
        return ExternalLoginAsync(value,
            _authOptions.MicrosoftId,
            _authOptions.MicrosoftSecret,
            (f, c) =>
            {
                c.Scope = "wl.emails";
                return new WindowsLiveClient(f, c);
            }
        );
    }

    /// <summary>
    /// Removes an external login provider from the account
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="providerUserId">The provider user id.</param>
    /// <response code="200">User Authentication Token</response>
    /// <response code="400">Invalid provider name.</response>
    [Consumes("application/json")]
    [HttpPost("unlink/{providerName:minlength(1)}")]
    public async Task<ActionResult<TokenResult>> RemoveExternalLoginAsync(string providerName, ValueFromBody<string> providerUserId)
    {
        var user = CurrentUser;
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("External Login").Tag(providerName).Identity(user.EmailAddress).Property("User", user).Property("Provider User Id", providerUserId?.Value).SetHttpContext(HttpContext));
        if (String.IsNullOrWhiteSpace(providerName) || String.IsNullOrWhiteSpace(providerUserId?.Value))
        {
            _logger.LogError("Remove external login failed for {EmailAddress}: Invalid Provider Name or Provider User Id", user.EmailAddress);
            return BadRequest("Invalid Provider Name or Provider User Id.");
        }

        if (user.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(user.Password))
        {
            _logger.LogError("Remove external login failed for {EmailAddress}: You must set a local password before removing your external login", user.EmailAddress);
            return BadRequest("You must set a local password before removing your external login.");
        }

        try
        {
            if (user.RemoveOAuthAccount(providerName, providerUserId.Value))
                await _userRepository.SaveAsync(user, o => o.Cache());
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error removing external login for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
            throw;
        }

        await ResetUserTokensAsync(user, nameof(RemoveExternalLoginAsync));

        _logger.UserRemovedExternalLogin(user.EmailAddress, providerName);
        return Ok(new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) });
    }

    /// <summary>
    /// Change password
    /// </summary>
    /// <response code="200">User Authentication Token</response>
    /// <response code="422">Validation error</response>
    [Consumes("application/json")]
    [HttpPost("change-password")]
    public async Task<ActionResult<TokenResult>> ChangePasswordAsync(ChangePasswordModel model)
    {
        var user = CurrentUser;
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Change Password").Identity(user.EmailAddress).Property("User", user).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(HttpContext));

        // User has a local account.
        if (!String.IsNullOrWhiteSpace(user.Password))
        {
            if (String.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                _logger.LogError("Change password failed for {EmailAddress}: The current password is incorrect", user.EmailAddress);
                ModelState.AddModelError<ChangePasswordModel>(m => m.CurrentPassword, "The current password is incorrect.");
                return ValidationProblem(ModelState);
            }

            string encodedPassword = model.CurrentPassword.ToSaltedHash(user.Salt!);
            if (!String.Equals(encodedPassword, user.Password))
            {
                _logger.LogError("Change password failed for {EmailAddress}: The current password is incorrect", user.EmailAddress);
                ModelState.AddModelError<ChangePasswordModel>(m => m.CurrentPassword, "The current password is incorrect.");
                return ValidationProblem(ModelState);

            }

            string newPasswordHash = model.Password!.ToSaltedHash(user.Salt!);
            if (String.Equals(newPasswordHash, user.Password))
            {
                _logger.LogError("Change password failed for {EmailAddress}: The new password is the same as the current password", user.EmailAddress);
                ModelState.AddModelError<ChangePasswordModel>(m => m.Password, "The new password must be different than the previous password.");
                return ValidationProblem(ModelState);
            }
        }

        await ChangePasswordAsync(user, model.Password!, nameof(ChangePasswordAsync));
        await ResetUserTokensAsync(user, nameof(ChangePasswordAsync));

        string userLoginAttemptsCacheKey = $"user:{user.EmailAddress}:attempts";
        await _cache.RemoveAsync(userLoginAttemptsCacheKey);

        string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
        long attempts = await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));
        if (attempts <= 0)
            await _cache.RemoveAsync(ipLoginAttemptsCacheKey);

        _logger.UserChangedPassword(user.EmailAddress);
        return Ok(new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) });
    }

    /// <summary>
    /// Checks to see if an Email Address is available for account creation
    /// </summary>
    /// <param name="email"></param>
    /// <response code="201">Email Address is available</response>
    /// <response code="204">Email Address is not available</response>
    [ApiExplorerSettings(IgnoreApi = true)]
    [AllowAnonymous]
    [HttpGet("check-email-address/{email:minlength(1)}")]
    public async Task<IActionResult> IsEmailAddressAvailableAsync(string email)
    {
        if (String.IsNullOrWhiteSpace(email))
            return StatusCode(StatusCodes.Status204NoContent);

        email = email.Trim().ToLowerInvariant();
        if (User.IsUserAuthType() && String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return StatusCode(StatusCodes.Status201Created);

        // Only allow 3 checks attempts per hour period by a single ip.
        string ipEmailAddressAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:email:attempts";
        long attempts = await _cache.IncrementAsync(ipEmailAddressAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));

        if (attempts > 3 || await _userRepository.GetByEmailAddressAsync(email) is null)
            return StatusCode(StatusCodes.Status204NoContent);

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>
    /// Forgot password
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <response code="200">Forgot password email was sent.</response>
    /// <response code="400">Invalid email address.</response>
    [AllowAnonymous]
    [HttpGet("forgot-password/{email:minlength(1)}")]
    public async Task<IActionResult> ForgotPasswordAsync(string email)
    {
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Forgot Password").Identity(email).SetHttpContext(HttpContext));
        if (String.IsNullOrWhiteSpace(email))
        {
            _logger.LogError("Forgot password failed: Please specify a valid Email Address");
            return BadRequest("Please specify a valid Email Address.");
        }

        // Only allow 3 checks attempts per hour period by a single ip.
        string ipResetPasswordAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:password:attempts";
        long attempts = await _cache.IncrementAsync(ipResetPasswordAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        if (attempts > 3)
        {
            _logger.LogError("Login denied for {EmailAddress} for the {ResetPasswordAttempts} time", email, attempts);
            return Ok();
        }

        email = email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAddressAsync(email);
        if (user is null)
        {
            _logger.LogError("Forgot password failed for {EmailAddress}: No user was found", email);
            return Ok();
        }

        user.CreatePasswordResetToken(_timeProvider);
        await _userRepository.SaveAsync(user, o => o.Cache());

        await _mailer.SendUserPasswordResetAsync(user);
        _logger.UserForgotPassword(user.EmailAddress);
        return Ok();
    }

    /// <summary>
    /// Reset password
    /// </summary>
    /// <response code="200">Password reset email was sent.</response>
    /// <response code="422">Invalid reset password model.</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordAsync(ResetPasswordModel model)
    {
        var user = await _userRepository.GetByPasswordResetTokenAsync(model.PasswordResetToken);
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Reset Password").Identity(user?.EmailAddress).Property("User", user).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(HttpContext));
        if (user is null)
        {
            _logger.LogError("Reset password failed: Invalid Password Reset Token");
            ModelState.AddModelError<ResetPasswordModel>(m => m.PasswordResetToken, "Invalid Password Reset Token");
            return ValidationProblem(ModelState);
        }

        if (!user.HasValidPasswordResetTokenExpiration(_timeProvider))
        {
            _logger.LogError("Reset password failed for {EmailAddress}: Password Reset Token has expired", user.EmailAddress);
            ModelState.AddModelError<ResetPasswordModel>(m => m.PasswordResetToken, "Password Reset Token has expired");
            return ValidationProblem(ModelState);
        }

        // User has a local account.
        if (!String.IsNullOrWhiteSpace(user.Password))
        {
            string newPasswordHash = model.Password!.ToSaltedHash(user.Salt!);
            if (String.Equals(newPasswordHash, user.Password))
            {
                _logger.LogError("Reset password failed for {EmailAddress}: The new password is the same as the current password", user.EmailAddress);
                ModelState.AddModelError<ResetPasswordModel>(m => m.Password, "The new password must be different than the previous password");
                return ValidationProblem(ModelState);
            }
        }

        user.MarkEmailAddressVerified();
        await ChangePasswordAsync(user, model.Password!, nameof(ResetPasswordAsync));
        await ResetUserTokensAsync(user, nameof(ResetPasswordAsync));

        string userLoginAttemptsCacheKey = $"user:{user.EmailAddress}:attempts";
        await _cache.RemoveAsync(userLoginAttemptsCacheKey);

        string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
        long attempts = await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromMinutes(15)));
        if (attempts <= 0)
            await _cache.RemoveAsync(ipLoginAttemptsCacheKey);

        _logger.UserResetPassword(user.EmailAddress);
        return Ok();
    }

    /// <summary>
    /// Cancel reset password
    /// </summary>
    /// <param name="token">The password reset token.</param>
    /// <response code="200">Password reset email was cancelled.</response>
    /// <response code="400">Invalid password reset token.</response>
    [AllowAnonymous]
    [Consumes("application/json")]
    [HttpPost("cancel-reset-password/{token:minlength(1)}")]
    public async Task<IActionResult> CancelResetPasswordAsync(string token)
    {
        if (String.IsNullOrEmpty(token))
        {
            using (_logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").SetHttpContext(HttpContext)))
                _logger.LogError("Cancel reset password failed: Invalid Password Reset Token");
            return BadRequest("Invalid password reset token.");
        }

        var user = await _userRepository.GetByPasswordResetTokenAsync(token);
        if (user is null)
            return Ok();

        user.ResetPasswordResetToken();
        await _userRepository.SaveAsync(user, o => o.Cache());

        using (_logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").Identity(user.EmailAddress).Property("User", user).SetHttpContext(HttpContext)))
            _logger.UserCanceledResetPassword(user.EmailAddress);

        return Ok();
    }

    private async Task AddGlobalAdminRoleIfFirstUserAsync(User user)
    {
        if (_isFirstUserChecked)
            return;

        bool isFirstUser = await _userRepository.CountAsync() == 0;
        if (isFirstUser)
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);

        _isFirstUserChecked = true;
    }

    private async Task<ActionResult<TokenResult>> ExternalLoginAsync<TClient>(ExternalAuthInfo authInfo, string? appId, string? appSecret, Func<IRequestFactory, IClientConfiguration, TClient> createClient) where TClient : OAuth2Client
    {
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("External Login").SetHttpContext(HttpContext));
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
            _logger.LogCritical(ex, "External login failed Code={AuthCode} RedirectUri={AuthRedirectUri}: {Message}", authInfo.Code, authInfo.RedirectUri, ex.Message);
            throw;
        }

        User? user;
        try
        {
            user = await FromExternalLoginAsync(userInfo);
        }
        catch (ApplicationException ex)
        {
            _logger.LogCritical(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
            return Forbidden("Account Creation is currently disabled");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
            throw;
        }

        if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
            await AddInvitedUserToOrganizationAsync(authInfo.InviteToken, user);

        _logger.UserLoggedIn(user.EmailAddress);
        return Ok(new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) });
    }

    private async Task<User> FromExternalLoginAsync(UserInfo userInfo)
    {
        var existingUser = await _userRepository.GetUserByOAuthProviderAsync(userInfo.ProviderName, userInfo.Id);
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("External Login").Property("User Info", userInfo).Property("ExistingUser", existingUser).SetHttpContext(HttpContext));

        // Link user accounts.
        if (User.IsUserAuthType())
        {
            var currentUser = CurrentUser;
            if (existingUser is not null)
            {
                if (existingUser.Id != currentUser.Id)
                {
                    // Existing user account is not the current user. Remove it, and we'll add it to the current user below.
                    if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id))
                    {
                        throw new Exception($"Unable to remove existing oauth account for existing user: {existingUser.EmailAddress}");
                    }

                    await _userRepository.SaveAsync(existingUser, o => o.Cache());
                }
                else
                {
                    // User is already logged in.
                    return currentUser;
                }
            }

            // Add it to the current user if it doesn't already exist and save it.
            currentUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
            return await _userRepository.SaveAsync(currentUser, o => o.Cache());
        }

        // Create a new user account or return an existing one.
        if (existingUser is not null)
        {
            if (!existingUser.IsEmailAddressVerified)
            {
                existingUser.MarkEmailAddressVerified();
                await _userRepository.SaveAsync(existingUser, o => o.Cache());
            }

            return existingUser;
        }

        // Check to see if a user already exists with this email address.
        var user = !String.IsNullOrEmpty(userInfo.Email) ? await _userRepository.GetByEmailAddressAsync(userInfo.Email) : null;
        if (user is null)
        {
            if (!_authOptions.EnableAccountCreation)
                throw new ApplicationException("Account Creation is currently disabled.");

            user = new User { FullName = userInfo.GetFullName(), EmailAddress = userInfo.Email };
            user.Roles.Add(AuthorizationRoles.Client);
            user.Roles.Add(AuthorizationRoles.User);
            await AddGlobalAdminRoleIfFirstUserAsync(user);
        }

        user.MarkEmailAddressVerified();
        user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);

        if (String.IsNullOrEmpty(user.Id))
            await _userRepository.AddAsync(user, o => o.Cache());
        else
            await _userRepository.SaveAsync(user, o => o.Cache());

        return user;
    }

    private async Task<bool> IsAccountCreationEnabledAsync(string? token)
    {
        if (_authOptions.EnableAccountCreation)
            return true;

        if (String.IsNullOrEmpty(token))
            return false;

        var organization = await _organizationRepository.GetByInviteTokenAsync(token);
        return organization is not null;
    }

    private async Task AddInvitedUserToOrganizationAsync(string? token, User user)
    {
        if (String.IsNullOrWhiteSpace(token))
            return;

        using var _ = _logger.BeginScope(new ExceptionlessState().Tag("Invite").Identity(user.EmailAddress).Property("User", user).SetHttpContext(HttpContext));
        var organization = await _organizationRepository.GetByInviteTokenAsync(token);
        var invite = organization?.GetInvite(token);
        if (organization is null || invite is null)
        {
            _logger.UnableToAddInvitedUserInvalidToken(user.EmailAddress, token);
            return;
        }

        if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase))
        {
            _logger.MarkedInvitedUserAsVerified(user.EmailAddress);
            user.MarkEmailAddressVerified();
            await _userRepository.SaveAsync(user, o => o.Cache());
        }

        if (!user.OrganizationIds.Contains(organization.Id))
        {
            _logger.UserJoinedFromInvite(user.EmailAddress);
            user.OrganizationIds.Add(organization.Id);
            await _userRepository.SaveAsync(user, o => o.Cache());
        }

        organization.Invites.Remove(invite);
        await _organizationRepository.SaveAsync(organization, o => o.Cache());
    }

    private async Task ChangePasswordAsync(User user, string password, string tag)
    {
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag(tag).Identity(user.EmailAddress).SetHttpContext(HttpContext));
        if (String.IsNullOrEmpty(user.Salt))
            user.Salt = Core.Extensions.StringExtensions.GetNewToken();

        user.Password = password.ToSaltedHash(user.Salt);
        user.ResetPasswordResetToken();

        try
        {
            await _userRepository.SaveAsync(user, o => o.Cache());
            _logger.ChangedUserPassword(user.EmailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error changing password for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
            throw;
        }
    }

    private async Task ResetUserTokensAsync(User user, string tag)
    {
        using var _ = _logger.BeginScope(new ExceptionlessState().Tag(tag).Identity(user.EmailAddress).SetHttpContext(HttpContext));
        try
        {
            long total = await _tokenRepository.RemoveAllByUserIdAsync(user.Id);
            _logger.RemovedUserTokens(total, user.EmailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Error removing user tokens for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
        }
    }

    private async Task<string> GetOrCreateAuthenticationTokenAsync(User user)
    {
        var userTokens = await _tokenRepository.GetByTypeAndUserIdAsync(TokenType.Authentication, user.Id);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var validAccessToken = userTokens.Documents.FirstOrDefault(t => (!t.ExpiresUtc.HasValue || t.ExpiresUtc > utcNow));
        if (validAccessToken is not null)
            return validAccessToken.Id;

        var token = await _tokenRepository.AddAsync(new Token
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

        string? domainUsername = _domainLoginProvider.GetUsernameFromEmailAddress(email);
        return domainUsername is not null && _domainLoginProvider.Login(domainUsername, password);
    }
}
