using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Models.OAuth;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/users")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class UserController : RepositoryApiController<IUserRepository, User, ViewUser, ViewUser, UpdateUser>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly ICacheClient _cache;
    private readonly IFileStorage _fileStorage;
    private readonly IMailer _mailer;
    private readonly IntercomOptions _intercomOptions;

    public UserController(
        IUserRepository userRepository, IFileStorage fileStorage,
        IOrganizationRepository organizationRepository, ITokenRepository tokenRepository, IOAuthApplicationRepository oauthApplicationRepository, ICacheClient cacheClient, IMailer mailer,
        ApiMapper mapper, IAppQueryValidator validator, IntercomOptions intercomOptions,
        TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(userRepository, mapper, validator, timeProvider, loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _tokenRepository = tokenRepository;
        _oauthApplicationRepository = oauthApplicationRepository;
        _cache = new ScopedCacheClient(cacheClient, "User");
        _fileStorage = fileStorage;
        _mailer = mailer;
        _intercomOptions = intercomOptions;
    }

    // Mapping implementations - User uses ViewUser as both TViewModel and TNewModel (no NewUser type)
    protected override User MapToModel(ViewUser newModel) => throw new NotSupportedException("Users cannot be created via API mapping.");
    protected override ViewUser MapToViewModel(User model) => _mapper.MapToViewUser(model);
    protected override List<ViewUser> MapToViewModels(IEnumerable<User> models) => _mapper.MapToViewUsers(models);

    /// <summary>
    /// Get current user
    /// </summary>
    /// <response code="404">The current user could not be found.</response>
    [HttpGet("me")]
    public async Task<ActionResult<ViewCurrentUser>> GetCurrentUserAsync()
    {
        var currentUser = await GetModelAsync(CurrentUser.Id);
        if (currentUser is null)
            return NotFound();

        return Ok(MapToViewCurrentUser(currentUser));
    }

    /// <summary>
    /// Get current user OAuth grants
    /// </summary>
    [HttpGet("me/oauth-grants")]
    public async Task<ActionResult<IReadOnlyCollection<ViewOAuthGrant>>> GetOAuthGrantsAsync()
    {
        var results = await _tokenRepository.GetOAuthAccessTokensByUserIdAsync(CurrentUser.Id, o => o.PageLimit(MAXIMUM_SKIP));
        var tokens = results.Documents.Where(IsActiveOAuthGrantToken).ToArray();
        if (tokens.Length == 0)
            return Ok(Array.Empty<ViewOAuthGrant>());

        var applicationsByClientId = new Dictionary<string, OAuthApplication?>(StringComparer.Ordinal);
        foreach (string clientId in tokens.Select(t => t.OAuthClientId!).Distinct(StringComparer.Ordinal))
            applicationsByClientId[clientId] = await _oauthApplicationRepository.GetByClientIdAsync(clientId);

        var grants = tokens
            .GroupBy(t => t.OAuthClientId!, StringComparer.Ordinal)
            .Select(group => MapToOAuthGrant(group.ToArray(), applicationsByClientId[group.Key]))
            .OrderBy(g => g.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.ClientId, StringComparer.Ordinal)
            .ToArray();

        return Ok(grants);
    }

    /// <summary>
    /// Revoke current user OAuth grant
    /// </summary>
    /// <param name="id">The representative OAuth access token id.</param>
    /// <response code="404">The OAuth grant could not be found.</response>
    [HttpDelete("me/oauth-grants/{id:minlength(1)}")]
    public async Task<IActionResult> RevokeOAuthGrantAsync(string id)
    {
        var token = await _tokenRepository.GetByIdAsync(id, o => o.ImmediateConsistency());
        if (token is null || token.OAuthType != OAuthTokenType.Access || !String.Equals(token.UserId, CurrentUser.Id, StringComparison.Ordinal) || String.IsNullOrWhiteSpace(token.OAuthClientId))
            return NotFound();

        var results = await _tokenRepository.GetOAuthAccessTokensByUserIdAndClientIdAsync(CurrentUser.Id, token.OAuthClientId, o => o.ImmediateConsistency().PageLimit(MAXIMUM_SKIP));
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var oauthToken in results.Documents.Where(t => t.OAuthType == OAuthTokenType.Access))
        {
            oauthToken.IsDisabled = true;
            oauthToken.Refresh = null;
            oauthToken.UpdatedUtc = utcNow;
            await _tokenRepository.SaveAsync(oauthToken, o => o.ImmediateConsistency());
        }

        return NoContent();
    }

    /// <summary>
    /// Get by id
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <response code="404">The user could not be found.</response>
    [HttpGet("{id:objectid}", Name = "GetUserById")]
    public Task<ActionResult<ViewUser>> GetAsync(string id)
    {
        return GetByIdImplAsync(id);
    }

    /// <summary>
    /// Get by organization
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="404">The organization could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/users")]
    public async Task<ActionResult<IReadOnlyCollection<ViewUser>>> GetByOrganizationAsync(string organizationId, int page = 1, int limit = 10)
    {
        if (!CanAccessOrganization(organizationId))
            return NotFound();

        var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache());
        if (organization is null)
            return NotFound();

        page = GetPage(page);
        limit = GetLimit(limit);
        int skip = GetSkip(page, limit);
        if (skip > MAXIMUM_SKIP)
            return Ok(Enumerable.Empty<ViewUser>());

        var results = await _repository.GetByOrganizationIdAsync(organizationId, o => o.PageLimit(MAXIMUM_SKIP));
        var users = MapToViewModels(results.Documents);
        await AfterResultMapAsync(users);
        if (!Request.IsGlobalAdmin())
            users.ForEach(u => u.Roles.Remove(AuthorizationRoles.GlobalAdmin));

        if (organization.Invites.Count > 0)
        {
            users.AddRange(organization.Invites.Select(i => new ViewUser
            {
                EmailAddress = i.EmailAddress,
                IsInvite = true
            }));
        }

        long total = results.Total + organization.Invites.Count;
        var pagedUsers = users.Skip(skip).Take(limit).ToList();
        return OkWithResourceLinks(pagedUsers, total > GetSkip(page + 1, limit), page, total);
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="changes">The changes</param>
    /// <response code="400">An error occurred while updating the user.</response>
    /// <response code="404">The user could not be found.</response>
    [HttpPatch("{id:objectid}")]
    [HttpPut("{id:objectid}")]
    [Consumes("application/json")]
    public Task<ActionResult<ViewUser>> PatchAsync(string id, Delta<UpdateUser> changes)
    {
        return PatchImplAsync(id, changes);
    }

    /// <summary>
    /// Upload avatar
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="file">The avatar image file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="404">The user could not be found.</response>
    /// <response code="422">The image file is invalid.</response>
    [HttpPost("{id:objectid}/avatar")]
    [Consumes("multipart/form-data")]
    [MultipartFileUpload]
    [RequestSizeLimit(ProfileImageStorage.MaxRequestBodySize)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileImageStorage.MaxRequestBodySize)]
    public async Task<ActionResult<ViewUser>> UploadAvatarAsync(string id, [FromForm] IFormFile? file, CancellationToken cancellationToken = default)
    {
        var user = await GetModelAsync(id, false);
        if (user is null)
            return NotFound();

        var image = await ProfileImageStorage.SaveAsync(_fileStorage, file, "users", user.Id, ModelState, cancellationToken);
        if (image is null)
            return ValidationProblem(ModelState);

        string? oldAvatarFileName = user.AvatarFileName;
        user.AvatarFileName = image.FileName;
        try
        {
            await _repository.SaveAsync(user, o => o.Cache());
        }
        catch
        {
            await ProfileImageStorage.TryDeleteAsync(_fileStorage, image.FileName, "users", user.Id, CancellationToken.None);
            throw;
        }

        await ProfileImageStorage.DeleteAsync(_fileStorage, oldAvatarFileName, "users", user.Id, cancellationToken);

        return await OkModelAsync(user);
    }

    /// <summary>
    /// Remove avatar
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="404">The user could not be found.</response>
    [HttpDelete("{id:objectid}/avatar")]
    public async Task<ActionResult<ViewUser>> DeleteAvatarAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await GetModelAsync(id, false);
        if (user is null)
            return NotFound();

        string? oldAvatarFileName = user.AvatarFileName;
        user.AvatarFileName = null;
        await _repository.SaveAsync(user, o => o.Cache());
        await ProfileImageStorage.DeleteAsync(_fileStorage, oldAvatarFileName, "users", user.Id, cancellationToken);

        return await OkModelAsync(user);
    }

    /// <summary>
    /// Get avatar
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="fileName">The avatar file name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="404">The avatar could not be found.</response>
    [AllowAnonymous]
    [HttpGet("{id:objectid}/avatar/{fileName}", Name = "GetUserAvatar")]
    [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAvatarAsync(string id, string fileName, CancellationToken cancellationToken = default)
    {
        if (!ProfileImageStorage.TryGetContentType(fileName, out string contentType))
            return NotFound();

        var stream = await ProfileImageStorage.GetFileStreamAsync(_fileStorage, fileName, "users", id, cancellationToken);
        return stream is null ? NotFound() : File(stream, contentType);
    }

    /// <summary>
    /// Delete current user
    /// </summary>
    /// <response code="404">The current user could not be found.</response>
    [HttpDelete("me")]
    [ProducesResponseType<WorkInProgressResult>(StatusCodes.Status202Accepted)]
    public Task<ActionResult<WorkInProgressResult>> DeleteCurrentUserAsync()
    {
        string[] userIds = !String.IsNullOrEmpty(CurrentUser.Id) ? [CurrentUser.Id] : [];
        return DeleteImplAsync(userIds);
    }

    /// <summary>
    /// Remove
    /// </summary>
    /// <param name="ids">A comma-delimited list of user identifiers.</param>
    /// <response code="202">Accepted</response>
    /// <response code="400">One or more validation errors occurred.</response>
    /// <response code="404">One or more users were not found.</response>
    /// <response code="500">An error occurred while deleting one or more users.</response>
    [HttpDelete("{ids:objectids}")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ProducesResponseType<WorkInProgressResult>(StatusCodes.Status202Accepted)]
    public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids)
    {
        return DeleteImplAsync(ids.FromDelimitedString());
    }

    /// <summary>
    /// Update email address
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <param name="email">The new email address.</param>
    /// <response code="400">An error occurred while updating the users email address.</response>
    /// <response code="422">Validation error</response>
    /// <response code="429">Update email address rate limit reached.</response>
    [HttpPost("{id:objectid}/email-address/{email:minlength(1)}")]
    public async Task<ActionResult<UpdateEmailAddressResult>> UpdateEmailAddressAsync(string id, string email)
    {
        var user = await GetModelAsync(id, false);
        if (user is null)
            return NotFound();

        using var _ = _logger.BeginScope(new ExceptionlessState().Property("User", user).SetHttpContext(HttpContext));

        email = email.Trim().ToLowerInvariant();
        if (String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });

        // Only allow 3 email address updates per hour period by a single user.
        string updateEmailAddressAttemptsCacheKey = $"{CurrentUser.Id}:attempts";
        long attempts = await _cache.IncrementAsync(updateEmailAddressAttemptsCacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        if (attempts > 3)
            return TooManyRequests("Unable to update email address. Please try later.");

        if (!await IsEmailAddressAvailableInternalAsync(email))
        {
            ModelState.AddModelError<User>(m => m.EmailAddress, "A user already exists with this email address.");
            return ValidationProblem(ModelState);
        }

        user.ResetPasswordResetToken();
        user.EmailAddress = email;
        user.IsEmailAddressVerified = user.OAuthAccounts.Any(oa => String.Equals(oa.EmailAddress(), email, StringComparison.InvariantCultureIgnoreCase));
        if (user.IsEmailAddressVerified)
            user.MarkEmailAddressVerified();
        else
            user.ResetVerifyEmailAddressTokenAndExpiration(_timeProvider);

        try
        {
            await _repository.SaveAsync(user, o => o.Cache());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user Email Address: {Message}", ex.Message);
            throw;
        }

        if (!user.IsEmailAddressVerified)
            await ResendVerificationEmailAsync(id);

        // TODO: We may want to send email to old email addresses as well.
        return Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });
    }

    /// <summary>
    /// Verify email address
    /// </summary>
    /// <param name="token">The token identifier.</param>
    /// <response code="404">The user could not be found.</response>
    /// <response code="422">Verify Email Address Token has expired.</response>
    [HttpGet("verify-email-address/{token:token}")]
    public async Task<IActionResult> VerifyAsync(string token)
    {
        var user = await _repository.GetByVerifyEmailAddressTokenAsync(token);
        if (user is null)
        {
            // The user may already be logged in and verified.
            if (CurrentUser.IsEmailAddressVerified)
                return Ok();

            return NotFound();
        }

        if (!user.HasValidVerifyEmailAddressTokenExpiration(_timeProvider))
        {
            ModelState.AddModelError<User>(m => m.VerifyEmailAddressTokenExpiration, "Verify Email Address Token has expired.");
            return ValidationProblem(ModelState);
        }

        user.MarkEmailAddressVerified();
        await _repository.SaveAsync(user, o => o.Cache());

        return Ok();
    }

    /// <summary>
    /// Resend verification email
    /// </summary>
    /// <param name="id">The identifier of the user.</param>
    /// <response code="200">The user verification email has been sent.</response>
    /// <response code="404">The user could not be found.</response>
    [HttpGet("{id:objectid}/resend-verification-email")]
    public async Task<IActionResult> ResendVerificationEmailAsync(string id)
    {
        var user = await GetModelAsync(id, false);
        if (user is null)
            return NotFound();

        if (!user.IsEmailAddressVerified)
        {
            user.ResetVerifyEmailAddressTokenAndExpiration(_timeProvider);
            await _repository.SaveAsync(user, o => o.Cache());
            await _mailer.SendUserEmailVerifyAsync(user);
        }

        return Ok();
    }

    [HttpPost("unverify-email-address")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Consumes("text/plain")]
    public async Task<IActionResult> UnverifyEmailAddressAsync()
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        string[] emailAddresses = (await reader.ReadToEndAsync()).SplitAndTrim([',']);

        foreach (string emailAddress in emailAddresses)
        {
            var user = await _repository.GetByEmailAddressAsync(emailAddress);
            if (user is null)
            {
                _logger.LogWarning("Unable to mark user with email address {EmailAddress} as unverified: User not Found", emailAddress);
                continue;
            }

            user.ResetVerifyEmailAddressTokenAndExpiration(_timeProvider);
            await _repository.SaveAsync(user, o => o.Cache());
            _logger.LogInformation("User {UserId} with email address {EmailAddress} is now unverified", user.Id, emailAddress);
        }

        return Ok();
    }

    [HttpPost("{id:objectid}/admin-role")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> AddAdminRoleAsync(string id)
    {
        var user = await GetModelAsync(id, false);
        if (user is null)
            return NotFound();

        if (!user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
        {
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);
            await _repository.SaveAsync(user, o => o.Cache());
        }

        return Ok();
    }

    [HttpDelete("{id:objectid}/admin-role")]
    [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> DeleteAdminRoleAsync(string id)
    {
        var user = await GetModelAsync(id, false);
        if (user is null)
            return NotFound();

        if (user.Roles.Remove(AuthorizationRoles.GlobalAdmin))
        {
            await _repository.SaveAsync(user, o => o.Cache());
        }

        return StatusCode(StatusCodes.Status204NoContent);
    }

    private async Task<bool> IsEmailAddressAvailableInternalAsync(string email)
    {
        if (String.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim().ToLowerInvariant();
        if (String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return true;

        return await _repository.GetByEmailAddressAsync(email) is null;
    }

    protected override async Task<ActionResult<ViewUser>> OkModelAsync(User model)
    {
        if (String.Equals(CurrentUser.Id, model.Id))
            return Ok(MapToViewCurrentUser(model));

        return await base.OkModelAsync(model);
    }

    protected override async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models)
    {
        await base.AfterResultMapAsync(models);

        foreach (var user in models.OfType<ViewUser>())
            user.AvatarUrl = GetUserAvatarUrl(user.Id, user.AvatarUrl);
    }

    protected override async Task<User?> GetModelAsync(string id, bool useCache = true)
    {
        if (Request.IsGlobalAdmin() || String.Equals(CurrentUser.Id, id))
            return await base.GetModelAsync(id, useCache);

        return null;
    }

    protected override Task<IReadOnlyCollection<User>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (Request.IsGlobalAdmin())
            return base.GetModelsAsync(ids, useCache);

        return base.GetModelsAsync(ids.Where(id => String.Equals(CurrentUser.Id, id)).ToArray(), useCache);
    }

    protected override async Task<PermissionResult> CanDeleteAsync(User value)
    {
        if (value.OrganizationIds.Count > 0)
            return PermissionResult.DenyWithMessage("Please delete or leave any organizations before deleting your account.");

        if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && value.Id != CurrentUser.Id)
            return PermissionResult.Deny;

        return await base.CanDeleteAsync(value);
    }

    protected override async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<User> values)
    {
        foreach (var user in values)
        {
            long removed = await _tokenRepository.RemoveAllByUserIdAsync(user.Id);
            _logger.RemovedTokens(removed, user.Id);
        }

        return await base.DeleteModelsAsync(values);
    }

    private ViewCurrentUser MapToViewCurrentUser(User user)
        => new(user, _intercomOptions) { AvatarUrl = GetUserAvatarUrl(user.Id, user.AvatarFileName) };

    private bool IsActiveOAuthGrantToken(Token token)
    {
        if (token is { OAuthType: not OAuthTokenType.Access } || token.IsDisabled || token.IsSuspended || String.IsNullOrWhiteSpace(token.OAuthClientId))
            return false;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        bool hasActiveAccessToken = !token.ExpiresUtc.HasValue || token.ExpiresUtc.Value >= utcNow;
        bool hasActiveRefreshToken = !String.IsNullOrEmpty(token.Refresh) && (!token.OAuthRefreshExpiresUtc.HasValue || token.OAuthRefreshExpiresUtc.Value >= utcNow);
        return hasActiveAccessToken || hasActiveRefreshToken;
    }

    private static ViewOAuthGrant MapToOAuthGrant(IReadOnlyCollection<Token> tokens, OAuthApplication? application)
    {
        var latestToken = tokens
            .OrderByDescending(t => t.UpdatedUtc)
            .ThenByDescending(t => t.CreatedUtc)
            .First();

        return new ViewOAuthGrant
        {
            Id = latestToken.Id,
            ClientId = latestToken.OAuthClientId!,
            ApplicationName = application?.Name ?? latestToken.OAuthClientId!,
            IsApplicationDisabled = application?.IsDisabled ?? false,
            Scopes = tokens
                .SelectMany(t => t.Scopes)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            OrganizationIds = tokens
                .SelectMany(t => t.OAuthOrganizationIds)
                .Where(id => !String.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Resources = tokens
                .Where(t => !String.IsNullOrWhiteSpace(t.OAuthResource))
                .GroupBy(t => t.OAuthResource!, StringComparer.Ordinal)
                .Select(group => new ViewOAuthGrantResource
                {
                    Resource = group.Key,
                    Scopes = group.SelectMany(t => t.Scopes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    OrganizationIds = group.SelectMany(t => t.OAuthOrganizationIds).Where(id => !String.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                })
                .OrderBy(r => r.Resource, StringComparer.Ordinal)
                .ToArray(),
            CreatedUtc = tokens.Min(t => t.CreatedUtc),
            UpdatedUtc = tokens.Max(t => t.UpdatedUtc),
            ExpiresUtc = tokens.Select(t => t.ExpiresUtc).Max(),
            RefreshExpiresUtc = tokens.Select(t => t.OAuthRefreshExpiresUtc).Max()
        };
    }

    private string? GetUserAvatarUrl(string id, string? fileName)
    {
        if (String.IsNullOrWhiteSpace(fileName))
            return null;

        return Url.RouteUrl("GetUserAvatar", new { id, fileName }) ?? $"/api/v2/users/{id}/avatar/{fileName}";
    }
}
