using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Mediator;
using Foundatio.Repositories;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;

namespace Exceptionless.Web.Api.Handlers;

public class TokenHandler(
    ITokenRepository repository,
    IProjectRepository projectRepository,
    ApiMapper mapper,
    IAppQueryValidator validator,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly IAppQueryValidator _validator = validator;
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<PagedResult<ViewToken>>> Handle(GetTokensByOrganization message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot access tokens.");

        if (String.IsNullOrEmpty(message.OrganizationId) || !HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.NotFound("Organization not found.");

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var tokens = await repository.GetByTypeAndOrganizationIdAsync(TokenType.Access, message.OrganizationId, o => o.PageNumber(page).PageLimit(limit));
        var viewTokens = mapper.MapToViewTokens(tokens.Documents);
        AfterResultMap(viewTokens);
        return new PagedResult<ViewToken>(viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
    }

    public async Task<Result<PagedResult<ViewToken>>> Handle(GetTokensByProject message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot access tokens.");

        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return Result.NotFound("Project not found.");

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var tokens = await repository.GetByTypeAndProjectIdAsync(TokenType.Access, message.ProjectId, o => o.PageNumber(page).PageLimit(limit));
        var viewTokens = mapper.MapToViewTokens(tokens.Documents);
        AfterResultMap(viewTokens);
        return new PagedResult<ViewToken>(viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
    }

    public async Task<Result<ViewToken>> Handle(GetDefaultToken message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot access tokens.");

        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return Result.NotFound("Project not found.");

        var defaultTokenResults = await repository.GetByTypeAndProjectIdAsync(TokenType.Access, message.ProjectId, o => o.PageLimit(1));
        var token = defaultTokenResults.Documents.FirstOrDefault();
        if (token is not null)
            return MapToView(token);

        return await CreateTokenImplAsync(new NewToken { OrganizationId = project.OrganizationId, ProjectId = message.ProjectId });
    }

    public async Task<Result<ViewToken>> Handle(GetTokenById message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot access tokens.");

        var model = await GetModelAsync(message.Id);
        if (model is null)
            return Result.NotFound("Token not found.");

        return MapToView(model);
    }

    public Task<Result<ViewToken>> Handle(CreateToken message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Task.FromResult<Result<ViewToken>>(Result.Forbidden("Token authentication cannot create tokens."));

        return CreateTokenImplAsync(message.Token);
    }

    public async Task<Result<ViewToken>> Handle(CreateTokenByProject message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot create tokens.");

        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return Result.NotFound("Project not found.");

        var token = message.Token ?? new NewToken();
        token.OrganizationId = project.OrganizationId;
        token.ProjectId = message.ProjectId;
        return await CreateTokenImplAsync(token);
    }

    public Task<Result<ViewToken>> Handle(CreateTokenByOrganization message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Task.FromResult<Result<ViewToken>>(Result.Forbidden("Token authentication cannot create tokens."));

        if (!HttpContext.Request.IsInOrganization(message.OrganizationId))
            return Task.FromResult<Result<ViewToken>>(Result.BadRequest("Invalid organization."));

        var token = message.Token ?? new NewToken();
        token.OrganizationId = message.OrganizationId;
        return CreateTokenImplAsync(token);
    }

    public async Task<Result<ViewToken>> Handle(UpdateTokenMessage message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot update tokens.");

        var original = await GetModelAsync(message.Id, useCache: false);
        if (original is null)
            return Result.NotFound("Token not found.");

        if (!message.Changes.GetChangedPropertyNames().Any())
            return MapToView(original);

        var error = CanUpdate(original, message.Changes);
        if (error is not null)
            return error;

        message.Changes.Patch(original);
        await repository.SaveAsync(original, o => o.Cache());
        return MapToView(original);
    }

    public async Task<Result<ModelActionResults>> Handle(DeleteTokens message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Result.Forbidden("Token authentication cannot delete tokens.");

        var items = await GetModelsAsync(message.Ids, useCache: false);
        if (items.Count == 0)
            return Result.NotFound("No tokens found.");

        var results = new ModelActionResults();
        results.AddNotFound(message.Ids.Except(items.Select(i => i.Id)));

        var deletableItems = items.ToList();
        foreach (var model in items)
        {
            var permission = await CanDeleteAsync(model);
            if (permission.Allowed)
                continue;

            deletableItems.Remove(model);
            results.Failure.Add(permission);
        }

        if (deletableItems.Count == 0)
        {
            if (results.Failure.Count == 1)
                return PermissionToResult(results.Failure.First());
            return results;
        }

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return results;

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return results;
    }

    private async Task<Result<ViewToken>> CreateTokenImplAsync(NewToken value)
    {
        if (value is null)
            return Result.BadRequest("Token value is required.");

        var mapped = mapper.MapToToken(value);
        if (String.IsNullOrEmpty(mapped.OrganizationId) && HttpContext.Request.GetAssociatedOrganizationIds().Count > 0)
            mapped.OrganizationId = HttpContext.Request.GetDefaultOrganizationId()!;

        var error = await CanAddAsync(mapped);
        if (error is not null)
            return error;

        var model = await AddModelAsync(mapped);
        var viewModel = mapper.MapToViewToken(model);
        AfterResultMap([viewModel]);
        return Result<ViewToken>.Created(viewModel, $"/api/v2/tokens/{model.Id}");
    }

    private async Task<Result<ViewToken>?> CanAddAsync(Token value)
    {
        if (String.IsNullOrEmpty(value.OrganizationId))
            return Result.Forbidden("Organization is required.");

        if (String.IsNullOrEmpty(value.ProjectId))
            return Result.Invalid(ValidationError.Create("project_id", "The project_id field is required."));

        bool hasUserRole = HttpContext.User.IsInRole(AuthorizationRoles.User);
        bool hasGlobalAdminRole = HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin);
        if (!hasGlobalAdminRole && !String.IsNullOrEmpty(value.UserId) && value.UserId != GetCurrentUserId())
            return Result.Forbidden("Cannot create tokens for other users.");

        if (!String.IsNullOrEmpty(value.ProjectId) && !String.IsNullOrEmpty(value.UserId))
            return Result.Invalid(ValidationError.Create("", "Token can't be associated to both user and project."));

        foreach (string scope in value.Scopes.ToList())
        {
            string lowerCaseScope = scope.ToLowerInvariant();
            if (!String.Equals(scope, lowerCaseScope, StringComparison.Ordinal))
            {
                value.Scopes.Remove(scope);
                value.Scopes.Add(lowerCaseScope);
            }

            if (!AuthorizationRoles.AllScopes.Contains(lowerCaseScope))
                return Result.Invalid(ValidationError.Create("scopes", "Invalid token scope requested."));
        }

        if (value.Scopes.Count == 0)
            value.Scopes.Add(AuthorizationRoles.Client);

        if ((value.Scopes.Contains(AuthorizationRoles.Client) && !hasUserRole)
            || (value.Scopes.Contains(AuthorizationRoles.User) && !hasUserRole)
            || (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin) && !hasGlobalAdminRole))
            return Result.Invalid(ValidationError.Create("scopes", "Invalid token scope requested."));

        if (!String.IsNullOrEmpty(value.ProjectId))
        {
            var project = await GetProjectAsync(value.ProjectId);
            if (project is null)
                return Result.Invalid(ValidationError.Create("project_id", "Please specify a valid project id."));

            value.OrganizationId = project.OrganizationId;
            value.DefaultProjectId = null;
        }

        if (!String.IsNullOrEmpty(value.DefaultProjectId))
        {
            var project = await GetProjectAsync(value.DefaultProjectId);
            if (project is null)
                return Result.Invalid(ValidationError.Create("default_project_id", "Please specify a valid default project id."));
        }

        if (!HttpContext.Request.CanAccessOrganization(value.OrganizationId))
            return Result.Invalid(ValidationError.Create("organization_id", "Invalid organization id specified."));

        return null;
    }

    private Task<Token> AddModelAsync(Token value)
    {
        value.Id = StringExtensions.GetNewToken();
        value.CreatedUtc = value.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        value.Type = TokenType.Access;
        value.CreatedBy = GetCurrentUserId();

        if (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin))
            value.Scopes.Add(AuthorizationRoles.User);

        if (value.Scopes.Contains(AuthorizationRoles.User))
            value.Scopes.Add(AuthorizationRoles.Client);

        return repository.AddAsync(value, o => o.Cache());
    }

    private async Task<PermissionResult> CanDeleteAsync(Token value)
    {
        if (!HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin) && !String.IsNullOrEmpty(value.UserId) && value.UserId != GetCurrentUserId())
            return PermissionResult.DenyWithMessage("Can only delete tokens created by you.");

        if (!String.IsNullOrEmpty(value.ProjectId) && !await IsInProjectAsync(value.ProjectId))
            return PermissionResult.DenyWithNotFound(value.Id);

        if (!HttpContext.Request.CanAccessOrganization(value.OrganizationId))
            return PermissionResult.DenyWithNotFound(value.Id);

        return PermissionResult.Allow;
    }

    private async Task<Token?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!String.IsNullOrEmpty(model.OrganizationId) && !HttpContext.Request.IsInOrganization(model.OrganizationId))
            return null;

        if (!HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin) && !String.IsNullOrEmpty(model.UserId) && model.UserId != GetCurrentUserId())
            return null;

        if (model.Type != TokenType.Access)
            return null;

        if (!String.IsNullOrEmpty(model.ProjectId) && !await IsInProjectAsync(model.ProjectId))
            return null;

        return model;
    }

    private async Task<IReadOnlyCollection<Token>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var models = await repository.GetByIdsAsync(ids, o => o.Cache(useCache));
        return models.Where(m => HttpContext.Request.CanAccessOrganization(m.OrganizationId)).ToList();
    }

    private async Task<Project?> GetProjectAsync(string projectId, bool useCache = true)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
        if (project is null || !HttpContext.Request.CanAccessOrganization(project.OrganizationId))
            return null;

        return project;
    }

    private async Task<bool> IsInProjectAsync(string projectId)
    {
        var project = await GetProjectAsync(projectId);
        return project is not null;
    }

    private ViewToken MapToView(Token model)
    {
        var viewModel = mapper.MapToViewToken(model);
        AfterResultMap([viewModel]);
        return viewModel;
    }

    private string GetCurrentUserId() => HttpContext.Request.GetUser().Id;

    private static void AfterResultMap<TDestination>(ICollection<TDestination> models)
    {
        foreach (var model in models.OfType<IData>())
            model.Data?.RemoveSensitiveData();
    }

    private static Result<ModelActionResults> PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode is StatusCodes.Status404NotFound)
            return Result.NotFound(permission.Message ?? "Not found.");

        return Result.Forbidden(permission.Message ?? "Access denied.");
    }

    private Result<ViewToken>? CanUpdate(Token original, Delta<UpdateToken> changes)
    {
        if (!HttpContext.Request.CanAccessOrganization(original.OrganizationId))
            return Result.Invalid(ValidationError.Create("organization_id", "Invalid organization id specified."));

        if (changes.GetChangedPropertyNames().Contains(nameof(Token.OrganizationId)))
            return Result.Invalid(ValidationError.Create("organization_id", "OrganizationId cannot be modified."));

        return null;
    }

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static bool NextPageExceedsSkipLimit(int page, int limit) => (page + 1) * limit >= 1000;
}
