﻿using AutoMapper;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.App.Controllers.API;

[Route(API_PREFIX + "/tokens")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class TokenController : RepositoryApiController<ITokenRepository, Token, ViewToken, NewToken, UpdateToken>
{
    private readonly IProjectRepository _projectRepository;

    public TokenController(ITokenRepository repository, IProjectRepository projectRepository, IMapper mapper, IAppQueryValidator validator, ILoggerFactory loggerFactory) : base(repository, mapper, validator, loggerFactory)
    {
        _projectRepository = projectRepository;
    }

    #region CRUD

    /// <summary>
    /// Get by organization
    /// </summary>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="404">The organization could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens")]
    public async Task<ActionResult<IReadOnlyCollection<ViewToken>>> GetByOrganizationAsync(string organizationId, int page = 1, int limit = 10)
    {
        if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
            return NotFound();

        page = GetPage(page);
        limit = GetLimit(limit);
        var tokens = await _repository.GetByTypeAndOrganizationIdAsync(TokenType.Access, organizationId, o => o.PageNumber(page).PageLimit(limit));
        var viewTokens = (await MapCollectionAsync<ViewToken>(tokens.Documents, true)).ToList();
        return OkWithResourceLinks(viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
    }

    /// <summary>
    /// Get by project
    /// </summary>
    /// <param name="projectId">The identifier of the project.</param>
    /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
    /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
    /// <response code="404">The project could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens")]
    public async Task<ActionResult<IReadOnlyCollection<ViewToken>>> GetByProjectAsync(string projectId, int page = 1, int limit = 10)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null)
            return NotFound();

        page = GetPage(page);
        limit = GetLimit(limit);
        var tokens = await _repository.GetByTypeAndProjectIdAsync(TokenType.Access, projectId, o => o.PageNumber(page).PageLimit(limit));
        var viewTokens = (await MapCollectionAsync<ViewToken>(tokens.Documents, true)).ToList();
        return OkWithResourceLinks(viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
    }

    /// <summary>
    /// Get a projects default token
    /// </summary>
    /// <param name="projectId">The identifier of the project.</param>
    /// <response code="404">The project could not be found.</response>
    [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens/default")]
    public async Task<ActionResult<ViewToken>> GetDefaultTokenAsync(string projectId)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null)
            return NotFound();

        var token = (await _repository.GetByTypeAndProjectIdAsync(TokenType.Access, projectId, o => o.PageLimit(1))).Documents.FirstOrDefault();
        if (token != null)
            return await OkModelAsync(token);

        return await PostImplAsync(new NewToken { OrganizationId = project.OrganizationId, ProjectId = projectId });
    }

    /// <summary>
    /// Get by id
    /// </summary>
    /// <param name="id">The identifier of the token.</param>
    /// <response code="404">The token could not be found.</response>
    [HttpGet("{id:token}", Name = "GetTokenById")]
    public Task<ActionResult<ViewToken>> GetAsync(string id)
    {
        return GetByIdImplAsync(id);
    }

    /// <summary>
    /// Create
    /// </summary>
    /// <remarks>
    /// To create a new token, you must specify an organization_id. There are three valid scopes: client, user and admin.
    /// </remarks>
    /// <param name="token">The token.</param>
    /// <response code="400">An error occurred while creating the token.</response>
    /// <response code="409">The token already exists.</response>
    [HttpPost]
    [Consumes("application/json")]
    public Task<ActionResult<ViewToken>> PostAsync(NewToken token)
    {
        return PostImplAsync(token);
    }

    /// <summary>
    /// Create for project
    /// </summary>
    /// <remarks>
    /// This is a helper action that makes it easier to create a token for a specific project.
    /// You may also specify a scope when creating a token. There are three valid scopes: client, user and admin.
    /// </remarks>
    /// <param name="projectId">The identifier of the project.</param>
    /// <param name="token">The token.</param>
    /// <response code="400">An error occurred while creating the token.</response>
    /// <response code="404">The project could not be found.</response>
    /// <response code="409">The token already exists.</response>
    [HttpPost("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens")]
    [Consumes("application/json")]
    public async Task<ActionResult<ViewToken>> PostByProjectAsync(string projectId, NewToken token = null)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null)
            return NotFound();

        if (token is null)
            token = new NewToken();

        token.OrganizationId = project.OrganizationId;
        token.ProjectId = projectId;
        return await PostImplAsync(token);
    }

    /// <summary>
    /// Create for organization
    /// </summary>
    /// <remarks>
    /// This is a helper action that makes it easier to create a token for a specific organization.
    /// You may also specify a scope when creating a token. There are three valid scopes: client, user and admin.
    /// </remarks>
    /// <param name="organizationId">The identifier of the organization.</param>
    /// <param name="token">The token.</param>
    /// <response code="400">An error occurred while creating the token.</response>
    /// <response code="409">The token already exists.</response>
    [HttpPost("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens")]
    [Consumes("application/json")]
    public async Task<ActionResult<ViewToken>> PostByOrganizationAsync(string organizationId, NewToken token = null)
    {
        if (token is null)
            token = new NewToken();

        if (!IsInOrganization(organizationId))
            return BadRequest();

        token.OrganizationId = organizationId;
        return await PostImplAsync(token);
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="id">The identifier of the token.</param>
    /// <param name="changes">The changes</param>
    /// <response code="400">An error occurred while updating the token.</response>
    /// <response code="404">The token could not be found.</response>
    [HttpPatch("{id:tokens}")]
    [HttpPut("{id:tokens}")]
    [Consumes("application/json")]
    public Task<ActionResult<ViewToken>> PatchAsync(string id, Delta<UpdateToken> changes)
    {
        return PatchImplAsync(id, changes);
    }

    /// <summary>
    /// Remove
    /// </summary>
    /// <param name="ids">A comma delimited list of token identifiers.</param>
    /// <response code="204">No Content.</response>
    /// <response code="400">One or more validation errors occurred.</response>
    /// <response code="404">One or more tokens were not found.</response>
    /// <response code="500">An error occurred while deleting one or more tokens.</response>
    [HttpDelete("{ids:tokens}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public Task<ActionResult<WorkInProgressResult>> DeleteAsync(string ids)
    {
        return DeleteImplAsync(ids.FromDelimitedString());
    }

    #endregion

    protected override async Task<Token> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await _repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model == null)
            return null;

        if (!String.IsNullOrEmpty(model.OrganizationId) && !IsInOrganization(model.OrganizationId))
            return null;

        if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && !String.IsNullOrEmpty(model.UserId) && model.UserId != CurrentUser.Id)
            return null;

        if (model.Type != TokenType.Access)
            return null;

        if (!String.IsNullOrEmpty(model.ProjectId) && !await IsInProjectAsync(model.ProjectId))
            return null;

        return model;
    }

    protected override async Task<PermissionResult> CanAddAsync(Token value)
    {
        // We only allow users to create organization scoped tokens.
        if (String.IsNullOrEmpty(value.OrganizationId))
            return PermissionResult.Deny;

        bool hasUserRole = User.IsInRole(AuthorizationRoles.User);
        bool hasGlobalAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin);
        if (!hasGlobalAdminRole && !String.IsNullOrEmpty(value.UserId) && value.UserId != CurrentUser.Id)
            return PermissionResult.Deny;

        if (!String.IsNullOrEmpty(value.ProjectId) && !String.IsNullOrEmpty(value.UserId))
            return PermissionResult.DenyWithMessage("Token can't be associated to both user and project.");

        foreach (string scope in value.Scopes.ToList())
        {
            string lowerCaseScoped = scope.ToLowerInvariant();
            if (!String.Equals(scope, lowerCaseScoped))
            {
                value.Scopes.Remove(scope);
                value.Scopes.Add(lowerCaseScoped);
            }

            if (!AuthorizationRoles.AllScopes.Contains(lowerCaseScoped))
                return PermissionResult.DenyWithMessage("Invalid token scope requested.");
        }

        if (value.Scopes.Count == 0)
            value.Scopes.Add(AuthorizationRoles.Client);

        if (value.Scopes.Contains(AuthorizationRoles.Client) && !hasUserRole)
            return PermissionResult.Deny;

        if (value.Scopes.Contains(AuthorizationRoles.User) && !hasUserRole)
            return PermissionResult.Deny;

        if (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin) && !hasGlobalAdminRole)
            return PermissionResult.Deny;

        if (!String.IsNullOrEmpty(value.ProjectId))
        {
            var project = await GetProjectAsync(value.ProjectId);
            if (project == null)
                return PermissionResult.Deny;

            value.OrganizationId = project.OrganizationId;
            value.DefaultProjectId = null;
        }

        if (!String.IsNullOrEmpty(value.DefaultProjectId))
        {
            var project = await GetProjectAsync(value.DefaultProjectId);
            if (project == null)
                return PermissionResult.Deny;
        }

        return await base.CanAddAsync(value);
    }

    protected override Task<Token> AddModelAsync(Token value)
    {
        value.Id = StringExtensions.GetNewToken();
        value.CreatedUtc = value.UpdatedUtc = SystemClock.UtcNow;
        value.Type = TokenType.Access;
        value.CreatedBy = Request.GetUser().Id;

        // add implied scopes
        if (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin) && !value.Scopes.Contains(AuthorizationRoles.User))
            value.Scopes.Add(AuthorizationRoles.User);

        if (value.Scopes.Contains(AuthorizationRoles.User) && !value.Scopes.Contains(AuthorizationRoles.Client))
            value.Scopes.Add(AuthorizationRoles.Client);

        return base.AddModelAsync(value);
    }

    protected override async Task<PermissionResult> CanDeleteAsync(Token value)
    {
        if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && !String.IsNullOrEmpty(value.UserId) && value.UserId != CurrentUser.Id)
            return PermissionResult.DenyWithMessage("Can only delete tokens created by you.");

        if (!String.IsNullOrEmpty(value.ProjectId) && !await IsInProjectAsync(value.ProjectId))
            return PermissionResult.DenyWithNotFound(value.Id);

        return await base.CanDeleteAsync(value);
    }

    private async Task<Project> GetProjectAsync(string projectId, bool useCache = true)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
        if (project == null || !CanAccessOrganization(project.OrganizationId))
            return null;

        return project;
    }

    private async Task<bool> IsInProjectAsync(string projectId)
    {
        var project = await GetProjectAsync(projectId);
        return project != null;
    }
}
