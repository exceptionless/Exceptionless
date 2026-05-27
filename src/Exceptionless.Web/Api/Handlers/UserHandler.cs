using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using HttpResults = Microsoft.AspNetCore.Http.Results;
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

    public async Task<IResult> Handle(GetCurrentUser message)
    {
        var currentUser = await GetModelAsync(GetCurrentUserId());
        if (currentUser is null)
            return HttpResults.NotFound();

        return HttpResults.Ok(new ViewCurrentUser(currentUser, intercomOptions));
    }

    public async Task<IResult> Handle(GetUserById message)
    {
        var model = await GetModelAsync(message.Id);
        if (model is null)
            return HttpResults.NotFound();

        return OkModel(model);
    }

    public async Task<IResult> Handle(GetUsersByOrganization message)
    {
        if (!HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return HttpResults.NotFound();

        var organization = await organizationRepository.GetByIdAsync(message.OrganizationId, o => o.Cache());
        if (organization is null)
            return HttpResults.NotFound();

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        int skip = GetSkip(page, limit);
        if (skip > 1000)
            return HttpResults.Ok(Enumerable.Empty<ViewUser>());

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
        return ApiResults.OkWithResourceLinks(HttpContext, pagedUsers, total > GetSkip(page + 1, limit), page, total);
    }

    public async Task<IResult> Handle(UpdateUserMessage message)
    {
        var original = await GetModelAsync(message.Id, useCache: false);
        if (original is null)
            return HttpResults.NotFound();

        if (!message.Changes.GetChangedPropertyNames().Any())
            return OkModel(original);

        var permission = CanUpdate(original, message.Changes);
        if (permission is not null)
            return permission;

        message.Changes.Patch(original);
        await repository.SaveAsync(original, o => o.Cache());
        return OkModel(original);
    }

    public Task<IResult> Handle(DeleteCurrentUser message)
    {
        string userId = GetCurrentUserId();
        string[] userIds = !String.IsNullOrEmpty(userId) ? [userId] : [];
        return DeleteImplAsync(userIds);
    }

    public Task<IResult> Handle(DeleteUsers message)
    {
        return DeleteImplAsync(message.Ids);
    }

    public async Task<IResult> Handle(UpdateEmailAddress message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return HttpResults.NotFound();

        using var _ = _logger.BeginScope(new ExceptionlessState().Property("User", user).SetHttpContext(HttpContext));

        string email = message.Email.Trim().ToLowerInvariant();
        var currentUser = HttpContext.Request.GetUser();
        if (String.Equals(currentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
            return HttpResults.Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });

        // Only allow 3 email address updates per hour period by a single user.
        string updateEmailAddressAttemptsCacheKey = $"{currentUser.Id}:attempts";
        long attempts = await _cache.IncrementAsync(updateEmailAddressAttemptsCacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        if (attempts > 3)
            return ApiResults.TooManyRequests("Unable to update email address. Please try later.");

        if (!await IsEmailAddressAvailableInternalAsync(email))
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email_address"] = ["A user already exists with this email address."]
            }, statusCode: StatusCodes.Status422UnprocessableEntity);

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

        return HttpResults.Ok(new UpdateEmailAddressResult { IsVerified = user.IsEmailAddressVerified });
    }

    public async Task<IResult> Handle(VerifyEmailAddress message)
    {
        var user = await repository.GetByVerifyEmailAddressTokenAsync(message.Token);
        if (user is null)
        {
            var currentUser = HttpContext.Request.GetUser();
            if (currentUser.IsEmailAddressVerified)
                return HttpResults.Ok();

            return HttpResults.NotFound();
        }

        if (!user.HasValidVerifyEmailAddressTokenExpiration(timeProvider))
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["verify_email_address_token_expiration"] = ["Verify Email Address Token has expired."]
            }, statusCode: StatusCodes.Status422UnprocessableEntity);

        user.MarkEmailAddressVerified();
        await repository.SaveAsync(user, o => o.Cache());

        return HttpResults.Ok();
    }

    public async Task<IResult> Handle(ResendVerificationEmail message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return HttpResults.NotFound();

        if (!user.IsEmailAddressVerified)
        {
            await ResendVerificationEmailInternalAsync(user);
        }

        return HttpResults.Ok();
    }

    public async Task<IResult> Handle(UnverifyEmailAddresses message)
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

        return HttpResults.Ok();
    }

    public async Task<IResult> Handle(AddAdminRole message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return HttpResults.NotFound();

        if (!user.Roles.Contains(AuthorizationRoles.GlobalAdmin))
        {
            user.Roles.Add(AuthorizationRoles.GlobalAdmin);
            await repository.SaveAsync(user, o => o.Cache());
        }

        return HttpResults.Ok();
    }

    public async Task<IResult> Handle(RemoveAdminRole message)
    {
        var user = await GetModelAsync(message.Id, false);
        if (user is null)
            return HttpResults.NotFound();

        if (user.Roles.Remove(AuthorizationRoles.GlobalAdmin))
        {
            await repository.SaveAsync(user, o => o.Cache());
        }

        return HttpResults.StatusCode(StatusCodes.Status204NoContent);
    }

    private async Task<IResult> DeleteImplAsync(string[] ids)
    {
        var items = await GetModelsAsync(ids, useCache: false);
        if (items.Count == 0)
            return HttpResults.NotFound();

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
            return results.Failure.Count == 1 ? PermissionToResult(results.Failure.First()) : HttpResults.BadRequest(results);

        foreach (var user in deletableItems)
        {
            long removed = await tokenRepository.RemoveAllByUserIdAsync(user.Id);
            _logger.RemovedTokens(removed, user.Id);
        }

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return TypedResults.Json(new WorkInProgressResult(), statusCode: StatusCodes.Status202Accepted);

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return HttpResults.BadRequest(results);
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

    private IResult OkModel(User model)
    {
        if (String.Equals(GetCurrentUserId(), model.Id))
        {
            var currentUserViewModel = new ViewCurrentUser(model, intercomOptions);
            AfterResultMap([currentUserViewModel]);
            return HttpResults.Ok(currentUserViewModel);
        }

        var viewModel = mapper.MapToViewUser(model);
        AfterResultMap([viewModel]);
        return HttpResults.Ok(viewModel);
    }

    private IResult? CanUpdate(User original, Delta<UpdateUser> changes)
    {
        // Users don't have a single OrganizationId - only check if not global admin and not self
        if (!HttpContext.Request.CanAccessOrganization(original.OrganizationIds.FirstOrDefault() ?? "")
            && !HttpContext.Request.IsGlobalAdmin() && original.Id != GetCurrentUserId())
            return PermissionToResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

        if (changes.GetChangedPropertyNames().Contains("OrganizationId"))
            return PermissionToResult(PermissionResult.DenyWithMessage("OrganizationId cannot be modified."));

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

    private static void AfterResultMap<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();
    }

    private static IResult PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode is StatusCodes.Status422UnprocessableEntity)
            return HttpResults.ValidationProblem(String.IsNullOrEmpty(permission.Message)
                ? new Dictionary<string, string[]>()
                : new Dictionary<string, string[]> { ["general"] = [permission.Message] },
                statusCode: StatusCodes.Status422UnprocessableEntity);

        if (String.IsNullOrEmpty(permission.Message))
            return TypedResults.Problem(statusCode: permission.StatusCode);

        return TypedResults.Problem(statusCode: permission.StatusCode, title: permission.Message);
    }

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static int GetSkip(int currentPage, int limit) => (currentPage < 1 ? 0 : (currentPage - 1)) * limit;
}
