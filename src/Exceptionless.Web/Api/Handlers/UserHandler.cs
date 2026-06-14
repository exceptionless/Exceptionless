using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;

namespace Exceptionless.Web.Api.Handlers;

public class UserHandler(
    IUserRepository repository,
    IOrganizationRepository organizationRepository,
    ITokenRepository tokenRepository,
    ICacheClient cacheClient,
    IMailer mailer,
    ApiMapper mapper,
    IntercomOptions intercomOptions,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory)
{
    private readonly ICacheClient _cache = new ScopedCacheClient(cacheClient, "User");
    private readonly ILogger _logger = loggerFactory.CreateLogger<UserHandler>();
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<ViewCurrentUser>> Handle(GetCurrentUser message)
    {
        var currentUser = await GetModelAsync(GetCurrentUserId());
        if (currentUser is null)
            return Result.NotFound("User not found.");

        return new ViewCurrentUser(currentUser, intercomOptions);
    }

    public async Task<Result<object>> Handle(GetUserById message)
    {
        var model = await GetModelAsync(message.Id);
        if (model is null)
            return Result.NotFound("User not found.");

        return Result<object>.Success(MapToView(model));
    }

    public async Task<Result<PagedResult<ViewUser>>> Handle(GetUsersByOrganization message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("User not found.");

        var organization = await organizationRepository.GetByIdAsync(message.OrganizationId, o => o.Cache());
        if (organization is null)
            return Result.NotFound("User not found.");

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        int skip = GetSkip(page, limit);
        if (skip > 1000)
            return new PagedResult<ViewUser>(Array.Empty<ViewUser>(), false, page, 0);

        var results = await repository.GetByOrganizationIdAsync(message.OrganizationId, o => o.PageLimit(1000));
        var users = mapper.MapToViewUsers(results.Documents);
        AfterResultMap(users);
        if (!HttpContext.Request.IsGlobalAdmin())
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
        return new PagedResult<ViewUser>(pagedUsers, total > GetSkip(page + 1, limit), page, total);
    }

    public async Task<Result<object>> Handle(UpdateUserMessage message)
    {
        var original = await GetModelAsync(message.Id, useCache: false);
        if (original is null)
            return Result.NotFound("User not found.");

        if (message.PatchDocument.IsEmpty())
            return Result<object>.Success(MapToView(original));

        var validationResult = JsonPatchValidation.ValidateOperations(message.PatchDocument, "/organization_id");
        if (!validationResult.IsSuccess)
            return Result<object>.FromResult(validationResult);

        var dto = new UpdateUser {
            FullName = original.FullName,
            EmailNotificationsEnabled = original.EmailNotificationsEnabled
        };

        var patchResult = JsonPatchValidation.ApplyPatch(message.PatchDocument, dto);
        if (!patchResult.IsSuccess)
            return Result<object>.FromResult(patchResult);

        var permission = CanUpdate(original, dto, message.PatchDocument);
        if (permission is not null)
            return permission;

        original.FullName = dto.FullName;
        original.EmailNotificationsEnabled = dto.EmailNotificationsEnabled;

        await repository.SaveAsync(original, o => o.Cache());
        return Result<object>.Success(MapToView(original));
    }

    public async Task<Result<ProfileImageUpdate<object>>> Handle(SetUserAvatar message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return Result.NotFound("User not found.");

        string? oldAvatarFileName = user.AvatarFileName;
        user.AvatarFileName = message.FileName;

        await repository.SaveAsync(user, o => o.Cache());
        return new ProfileImageUpdate<object>(MapToView(user), oldAvatarFileName);
    }

    public async Task<Result<ProfileImageUpdate<object>>> Handle(DeleteUserAvatar message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return Result.NotFound("User not found.");

        string? oldAvatarFileName = user.AvatarFileName;
        user.AvatarFileName = null;

        await repository.SaveAsync(user, o => o.Cache());
        return new ProfileImageUpdate<object>(MapToView(user), oldAvatarFileName);
    }

    public Task<Result<ModelActionResults>> Handle(DeleteCurrentUser message)
    {
        string userId = GetCurrentUserId();
        string[] userIds = !String.IsNullOrEmpty(userId) ? [userId] : [];
        return DeleteImplAsync(userIds);
    }

    public Task<Result<ModelActionResults>> Handle(DeleteUsers message)
    {
        return DeleteImplAsync(message.Ids);
    }

    public async Task<Result<UpdateEmailAddressResult>> Handle(UpdateEmailAddress message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return Result.NotFound("User not found.");

        using var _ = _logger.BeginScope(new ExceptionlessState().Property("User", user).SetHttpContext(HttpContext));

        string email = message.Email.Trim().ToLowerInvariant();
        var currentUser = HttpContext.Request.GetUser();
        if (String.Equals(currentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified };

        // Only allow 3 email address updates per hour period by a single user.
        string updateEmailAddressAttemptsCacheKey = $"{currentUser.Id}:attempts";
        long attempts = await _cache.IncrementAsync(updateEmailAddressAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        if (attempts > 3)
            return Result.Invalid(ValidationError.Create("rate_limit", "Unable to update email address. Please try later."));

        if (!await IsEmailAddressAvailableInternalAsync(email))
            return Result.Invalid(ValidationError.Create("email_address", "A user already exists with this email address."));

        user.ResetPasswordResetToken();
        user.EmailAddress = email;
        user.IsEmailAddressVerified = user.OAuthAccounts.Any(oa => String.Equals(oa.EmailAddress(), email, StringComparison.InvariantCultureIgnoreCase));
        if (user.IsEmailAddressVerified)
            user.MarkEmailAddressVerified();
        else
            user.ResetVerifyEmailAddressTokenAndExpiration(timeProvider);

        try
        {
            await repository.SaveAsync(user, o => o.Cache());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user Email Address: {Message}", ex.Message);
            throw;
        }

        if (!user.IsEmailAddressVerified)
            await ResendVerificationEmailInternalAsync(user);

        return new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified };
    }

    public async Task<Result> Handle(VerifyEmailAddress message)
    {
        var user = await repository.GetByVerifyEmailAddressTokenAsync(message.Token);
        if (user is null)
        {
            var currentUser = HttpContext.Request.GetUser();
            if (currentUser.IsEmailAddressVerified)
                return Result.Success();

            return Result.NotFound("User not found.");
        }

        if (!user.HasValidVerifyEmailAddressTokenExpiration(timeProvider))
            return Result.Invalid(ValidationError.Create("verify_email_address_token_expiration", "Verify Email Address Token has expired."));

        user.MarkEmailAddressVerified();
        await repository.SaveAsync(user, o => o.Cache());

        return Result.Success();
    }

    public async Task<Result> Handle(ResendVerificationEmail message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return Result.NotFound("User not found.");

        if (!user.IsEmailAddressVerified)
        {
            await ResendVerificationEmailInternalAsync(user);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(UnverifyEmailAddresses message)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        string[] emailAddresses = (await reader.ReadToEndAsync()).SplitAndTrim([',']);

        foreach (string emailAddress in emailAddresses)
        {
            var user = await repository.GetByEmailAddressAsync(emailAddress);
            if (user is null)
            {
                _logger.LogWarning("Unable to mark user with email address {EmailAddress} as unverified: User not Found", emailAddress);
                continue;
            }

            user.ResetVerifyEmailAddressTokenAndExpiration(timeProvider);
            await repository.SaveAsync(user, o => o.Cache());
            _logger.LogInformation("User {UserId} with email address {EmailAddress} is now unverified", user.Id, emailAddress);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(AddAdminRole message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return Result.NotFound("User not found.");

        if (!user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
        {
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);
            await repository.SaveAsync(user, o => o.Cache());
        }

        return Result.Success();
    }

    public async Task<Result> Handle(RemoveAdminRole message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return Result.NotFound("User not found.");

        if (user.Roles.Remove(AuthorizationRoles.GlobalAdmin))
        {
            await repository.SaveAsync(user, o => o.Cache());
        }

        return Result.NoContent();
    }

    private async Task<Result<ModelActionResults>> DeleteImplAsync(string[] ids)
    {
        var items = await GetModelsAsync(ids, useCache: false);
        if (items.Count == 0)
            return Result.NotFound("User not found.");

        var results = new ModelActionResults();
        results.AddNotFound(ids.Except(items.Select(i => i.Id)));

        var deletableItems = items.ToList();
        foreach (var model in items)
        {
            var permission = CanDelete(model);
            if (permission.Allowed)
                continue;

            deletableItems.Remove(model);
            results.Failure.Add(permission);
        }

        if (deletableItems.Count == 0)
            return results.Failure.Count == 1 ? Result<ModelActionResults>.FromResult(PermissionToResult(results.Failure.First())) : results;

        foreach (var user in deletableItems)
        {
            long removed = await tokenRepository.RemoveAllByUserIdAsync(user.Id);
            _logger.RemovedTokens(removed, user.Id);
        }

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return new ModelActionResults();

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return results;
    }

    private PermissionResult CanDelete(User value)
    {
        if (value.OrganizationIds.Count > 0)
            return PermissionResult.DenyWithMessage("Please delete or leave any organizations before deleting your account.");

        if (!HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin) && value.Id != GetCurrentUserId())
            return PermissionResult.Deny;

        return PermissionResult.Allow;
    }

    private async Task<User?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        if (HttpContext.Request.IsGlobalAdmin() || String.Equals(GetCurrentUserId(), id))
        {
            return await repository.GetByIdAsync(id, o => o.Cache(useCache));
        }

        return null;
    }

    private async Task<IReadOnlyCollection<User>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        if (HttpContext.Request.IsGlobalAdmin())
        {
            var models = await repository.GetByIdsAsync(ids, o => o.Cache(useCache));
            return models.ToList();
        }

        string currentUserId = GetCurrentUserId();
        var filteredIds = ids.Where(id => String.Equals(currentUserId, id)).ToArray();
        if (filteredIds.Length == 0)
            return [];

        var filteredModels = await repository.GetByIdsAsync(filteredIds, o => o.Cache(useCache));
        return filteredModels.ToList();
    }

    private object MapToView(User model)
    {
        if (String.Equals(GetCurrentUserId(), model.Id))
        {
            var currentUserViewModel = new ViewCurrentUser(model, intercomOptions);
            AfterResultMap([currentUserViewModel]);
            return currentUserViewModel;
        }

        var viewModel = mapper.MapToViewUser(model);
        AfterResultMap([viewModel]);
        return viewModel;
    }

    private Result<object>? CanUpdate(User original, UpdateUser dto, JsonPatchDocument<UpdateUser> patch)
    {
        // Users don't have a single OrganizationId - only check if not global admin and not self
        if (!HttpContext.Request.CanAccessOrganization(original.OrganizationIds.FirstOrDefault() ?? "")
            && !HttpContext.Request.IsGlobalAdmin() && original.Id != GetCurrentUserId())
            return Result<object>.FromResult(Result.Invalid(ValidationError.Create("organization_id", "Invalid organization id specified.")));

        if (patch.AffectsPath("/organization_id"))
            return Result<object>.FromResult(Result.Invalid(ValidationError.Create("organization_id", "OrganizationId cannot be modified.")));

        return null;
    }

    private async Task ResendVerificationEmailInternalAsync(User user)
    {
        user.ResetVerifyEmailAddressTokenAndExpiration(timeProvider);
        await repository.SaveAsync(user, o => o.Cache());
        await mailer.SendUserEmailVerifyAsync(user);
    }

    private async Task<bool> IsEmailAddressAvailableInternalAsync(string email)
    {
        if (String.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim().ToLowerInvariant();
        var currentUser = HttpContext.Request.GetUser();
        if (String.Equals(currentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return true;

        return await repository.GetByEmailAddressAsync(email) is null;
    }

    private string GetCurrentUserId() => HttpContext.Request.GetUser().Id;

    private void AfterResultMap<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();

        foreach (var user in models.OfType<ViewUser>())
            user.AvatarUrl = GetUserAvatarUrl(user.Id, user.AvatarUrl);
    }

    private static string? GetUserAvatarUrl(string id, string? fileName)
    {
        if (String.IsNullOrWhiteSpace(fileName))
            return null;

        return $"/api/v2/users/{id}/avatar/{fileName}";
    }

    private static Result PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode is StatusCodes.Status404NotFound)
            return Result.NotFound(permission.Message ?? "User not found.");

        if (permission.StatusCode is StatusCodes.Status422UnprocessableEntity)
            return Result.Invalid(ValidationError.Create("general", permission.Message ?? "Validation failed."));

        return Result.Forbidden(permission.Message ?? "Access denied.");
    }

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static int GetSkip(int currentPage, int limit) => (currentPage < 1 ? 0 : (currentPage - 1)) * limit;
}
