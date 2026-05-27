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
using Foundatio.Repositories;
using HttpResults = Microsoft.AspNetCore.Http.Results;
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

    public async Task<IResult> Handle(GetTokensByOrganization message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

        if (String.IsNullOrEmpty(message.OrganizationId) || !HttpContext.Request.CanAccessOrganization(message.OrganizationId))
            return HttpResults.NotFound();

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var tokens = await repository.GetByTypeAndOrganizationIdAsync(TokenType.Access, message.OrganizationId, o => o.PageNumber(page).PageLimit(limit));
        var viewTokens = mapper.MapToViewTokens(tokens.Documents);
        AfterResultMap(viewTokens);
        return ApiResults.OkWithResourceLinks(HttpContext, viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
    }

    public async Task<IResult> Handle(GetTokensByProject message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return HttpResults.NotFound();

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var tokens = await repository.GetByTypeAndProjectIdAsync(TokenType.Access, message.ProjectId, o => o.PageNumber(page).PageLimit(limit));
        var viewTokens = mapper.MapToViewTokens(tokens.Documents);
        AfterResultMap(viewTokens);
        return ApiResults.OkWithResourceLinks(HttpContext, viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
    }

    public async Task<IResult> Handle(GetDefaultToken message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return HttpResults.NotFound();

        var defaultTokenResults = await repository.GetByTypeAndProjectIdAsync(TokenType.Access, message.ProjectId, o => o.PageLimit(1));
        var token = defaultTokenResults.Documents.FirstOrDefault();
        if (token is not null)
            return OkModel(token);

        return await PostImplAsync(new NewToken { OrganizationId = project.OrganizationId, ProjectId = message.ProjectId });
    }

    public async Task<IResult> Handle(GetTokenById message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

        var model = await GetModelAsync(message.Id);
        return model is null ? HttpResults.NotFound() : OkModel(model);
    }

    public Task<IResult> Handle(CreateToken message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Task.FromResult<IResult>(HttpResults.Forbid());

        return PostImplAsync(message.Token);
    }

    public async Task<IResult> Handle(CreateTokenByProject message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return HttpResults.NotFound();

        var token = message.Token ?? new NewToken();
        token.OrganizationId = project.OrganizationId;
        token.ProjectId = message.ProjectId;
        return await PostImplAsync(token);
    }

    public Task<IResult> Handle(CreateTokenByOrganization message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return Task.FromResult<IResult>(HttpResults.Forbid());

        if (!HttpContext.Request.IsInOrganization(message.OrganizationId))
            return Task.FromResult<IResult>(HttpResults.BadRequest());

        var token = message.Token ?? new NewToken();
        token.OrganizationId = message.OrganizationId;
        return PostImplAsync(token);
    }

    public async Task<IResult> Handle(UpdateTokenMessage message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

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

    public async Task<IResult> Handle(DeleteTokens message)
    {
        if (HttpContext.User.IsTokenAuthType())
            return HttpResults.Forbid();

        var items = await GetModelsAsync(message.Ids, useCache: false);
        if (items.Count == 0)
            return HttpResults.NotFound();

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
            return results.Failure.Count == 1 ? PermissionToResult(results.Failure.First()) : HttpResults.BadRequest(results);

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return TypedResults.Json(new WorkInProgressResult(), statusCode: StatusCodes.Status202Accepted);

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return HttpResults.BadRequest(results);
    }

    private async Task<IResult> PostImplAsync(NewToken value)
    {
        if (value is null)
            return HttpResults.BadRequest();

        // ProjectId is required for direct token creation (mirrors old MVC implicit-required behavior)
        if (String.IsNullOrEmpty(value.ProjectId))
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["project_id"] = ["The project_id field is required."] });

        var mapped = mapper.MapToToken(value);
        if (String.IsNullOrEmpty(mapped.OrganizationId) && HttpContext.Request.GetAssociatedOrganizationIds().Count > 0)
            mapped.OrganizationId = HttpContext.Request.GetDefaultOrganizationId()!;

        var permission = await CanAddAsync(mapped);
        if (permission is not null)
            return permission;

        var model = await AddModelAsync(mapped);
        var viewModel = mapper.MapToViewToken(model);
        AfterResultMap([viewModel]);
        return TypedResults.Created($"/api/v2/tokens/{model.Id}", viewModel);
    }

    private async Task<IResult?> CanAddAsync(Token value)
    {
        if (String.IsNullOrEmpty(value.OrganizationId))
            return PermissionToResult(PermissionResult.Deny);

        bool hasUserRole = HttpContext.User.IsInRole(AuthorizationRoles.User);
        bool hasGlobalAdminRole = HttpContext.User.IsInRole(AuthorizationRoles.GlobalAdmin);
        if (!hasGlobalAdminRole && !String.IsNullOrEmpty(value.UserId) && value.UserId != GetCurrentUserId())
            return PermissionToResult(PermissionResult.Deny);

        if (!String.IsNullOrEmpty(value.ProjectId) && !String.IsNullOrEmpty(value.UserId))
            return PermissionToResult(PermissionResult.DenyWithMessage("Token can't be associated to both user and project."));

        foreach (string scope in value.Scopes.ToList())
        {
            string lowerCaseScope = scope.ToLowerInvariant();
            if (!String.Equals(scope, lowerCaseScope, StringComparison.Ordinal))
            {
                value.Scopes.Remove(scope);
                value.Scopes.Add(lowerCaseScope);
            }

            if (!AuthorizationRoles.AllScopes.Contains(lowerCaseScope))
                return ValidationProblem("scopes", "Invalid token scope requested.");
        }

        if (value.Scopes.Count == 0)
            value.Scopes.Add(AuthorizationRoles.Client);

        if ((value.Scopes.Contains(AuthorizationRoles.Client) && !hasUserRole)
            || (value.Scopes.Contains(AuthorizationRoles.User) && !hasUserRole)
            || (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin) && !hasGlobalAdminRole))
            return ValidationProblem("scopes", "Invalid token scope requested.");

        if (!String.IsNullOrEmpty(value.ProjectId))
        {
            var project = await GetProjectAsync(value.ProjectId);
            if (project is null)
                return ValidationProblem("project_id", "Please specify a valid project id.");

            value.OrganizationId = project.OrganizationId;
            value.DefaultProjectId = null;
        }

        if (!String.IsNullOrEmpty(value.DefaultProjectId))
        {
            var project = await GetProjectAsync(value.DefaultProjectId);
            if (project is null)
                return ValidationProblem("default_project_id", "Please specify a valid default project id.");
        }

        // Organization access check comes last (matches old base.CanAddAsync order)
        if (!HttpContext.Request.CanAccessOrganization(value.OrganizationId))
            return PermissionToResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

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

    private IResult OkModel(Token model)
    {
        var viewModel = mapper.MapToViewToken(model);
        AfterResultMap([viewModel]);
        return HttpResults.Ok(viewModel);
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

    private static IResult ValidationProblem(string key, string error)
        => Microsoft.AspNetCore.Http.Results.ValidationProblem(
            new Dictionary<string, string[]> { [key] = [error] },
            statusCode: StatusCodes.Status422UnprocessableEntity);

    private IResult? CanUpdate(Token original, Delta<UpdateToken> changes)
    {
        if (!HttpContext.Request.CanAccessOrganization(original.OrganizationId))
            return PermissionToResult(PermissionResult.DenyWithMessage("Invalid organization id specified."));

        if (changes.GetChangedPropertyNames().Contains(nameof(Token.OrganizationId)))
            return PermissionToResult(PermissionResult.DenyWithMessage("OrganizationId cannot be modified."));

        return null;
    }

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static bool NextPageExceedsSkipLimit(int page, int limit) => (page + 1) * limit >= 1000;
}
