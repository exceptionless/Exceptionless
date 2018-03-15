using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.App.Controllers.API {
    [Route(API_PREFIX + "/tokens")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class TokenController : RepositoryApiController<ITokenRepository, Token, ViewToken, NewToken, UpdateToken> {
        private readonly IProjectRepository _projectRepository;

        public TokenController(ITokenRepository repository, IProjectRepository projectRepository, IMapper mapper, IQueryValidator validator, ILoggerFactory loggerFactory) : base(repository, mapper, validator, loggerFactory) {
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
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<ViewToken>))]
        public async Task<IActionResult> GetByOrganizationAsync(string organizationId, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
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
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<ViewToken>))]
        public async Task<IActionResult> GetByProjectAsync(string projectId, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
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
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewToken))]
        public async Task<IActionResult> GetDefaultTokenAsync(string projectId) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var token = (await _repository.GetByTypeAndProjectIdAsync(TokenType.Access, projectId, o => o.PageLimit(1))).Documents.FirstOrDefault();
            if (token != null)
                return await OkModelAsync(token);

            return await PostImplAsync(new NewToken { OrganizationId = project.OrganizationId, ProjectId = projectId});
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the token.</param>
        /// <response code="404">The token could not be found.</response>
        [HttpGet("{id:token}", Name = "GetTokenById")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewToken))]
        public Task<IActionResult> GetByIdAsync(string id) {
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
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewToken))]
        public Task<IActionResult> PostAsync([FromBody] NewToken token) {
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
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewToken))]
        public async Task<IActionResult> PostByProjectAsync(string projectId, [FromBody] NewToken token) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            if (token == null)
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
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewToken))]
        public async Task<IActionResult> PostByOrganizationAsync(string organizationId, [FromBody] NewToken token) {
            if (token == null)
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
        public Task<IActionResult> PatchAsync(string id, [FromBody] Delta<UpdateToken> changes) {
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
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public Task<IActionResult> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        #endregion

        protected override async Task<Token> GetModelAsync(string id, bool useCache = true) {
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

        protected override async Task<PermissionResult> CanAddAsync(Token value) {
            // We only allow users to create organization scoped tokens.
            if (String.IsNullOrEmpty(value.OrganizationId))
                return PermissionResult.Deny;

            bool hasUserRole = User.IsInRole(AuthorizationRoles.User);
            bool hasGlobalAdminRole = User.IsInRole(AuthorizationRoles.GlobalAdmin);
            if (!hasGlobalAdminRole && !String.IsNullOrEmpty(value.UserId) && value.UserId != CurrentUser.Id)
                return PermissionResult.Deny;

            if (!String.IsNullOrEmpty(value.ProjectId) && !String.IsNullOrEmpty(value.UserId))
                return PermissionResult.DenyWithMessage("Token can't be associated to both user and project.");

            foreach (string scope in value.Scopes.ToList()) {
                if (scope != scope.ToLowerInvariant()) {
                    value.Scopes.Remove(scope);
                    value.Scopes.Add(scope.ToLowerInvariant());
                }

                if (!AuthorizationRoles.AllScopes.Contains(scope.ToLowerInvariant()))
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

            if (!String.IsNullOrEmpty(value.ProjectId)) {
                var project = await GetProjectAsync(value.ProjectId);
                if (project == null)
                    return PermissionResult.Deny;

                value.OrganizationId = project.OrganizationId;
                value.DefaultProjectId = null;
            }

            if (!String.IsNullOrEmpty(value.DefaultProjectId)) {
                var project = await GetProjectAsync(value.DefaultProjectId);
                if (project == null)
                    return PermissionResult.Deny;
            }

            return await base.CanAddAsync(value);
        }

        protected override Task<Token> AddModelAsync(Token value) {
            value.Id = StringExtensions.GetNewToken();
            value.CreatedUtc = value.UpdatedUtc = SystemClock.UtcNow;
            value.Type = TokenType.Access;
            value.CreatedBy = Request.GetUser().Id;

            // add implied scopes
            if (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin))
                value.Scopes.Add(AuthorizationRoles.User);

            if (value.Scopes.Contains(AuthorizationRoles.User))
                value.Scopes.Add(AuthorizationRoles.Client);

            return base.AddModelAsync(value);
        }

        protected override async Task<PermissionResult> CanDeleteAsync(Token value) {
            if (!User.IsInRole(AuthorizationRoles.GlobalAdmin) && !String.IsNullOrEmpty(value.UserId) &&  value.UserId != CurrentUser.Id)
                return PermissionResult.DenyWithMessage("Can only delete tokens created by you.");

            if (!String.IsNullOrEmpty(value.ProjectId) && !await IsInProjectAsync(value.ProjectId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return await base.CanDeleteAsync(value);
        }

        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return null;

            return project;
        }

        private async Task<bool> IsInProjectAsync(string projectId) {
            var project = await GetProjectAsync(projectId);
            return project != null;
        }
    }
}
