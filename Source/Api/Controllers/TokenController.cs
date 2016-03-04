using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Logging;
using Foundatio.Repositories.Models;

namespace Exceptionless.App.Controllers.API {
    [RoutePrefix(API_PREFIX + "/tokens")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class TokenController : RepositoryApiController<ITokenRepository, Token, ViewToken, NewToken, Token> {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IProjectRepository _projectRepository;

        public TokenController(ITokenRepository repository, IApplicationRepository applicationRepository, IProjectRepository projectRepository, ILoggerFactory loggerFactory, IMapper mapper) : base(repository, loggerFactory, mapper) {
            _applicationRepository = applicationRepository;
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
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens")]
        [ResponseType(typeof(List<ViewToken>))]
        public async Task<IHttpActionResult> GetByOrganizationAsync(string organizationId, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var tokens = await _repository.GetByTypeAndOrganizationIdAsync(TokenType.Access, organizationId, options, true);
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
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens")]
        [ResponseType(typeof(List<ViewToken>))]
        public async Task<IHttpActionResult> GetByProjectAsync(string projectId, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var tokens = await _repository.GetByTypeAndProjectIdAsync(TokenType.Access, projectId, options);
            var viewTokens = (await MapCollectionAsync<ViewToken>(tokens.Documents, true)).ToList();
            return OkWithResourceLinks(viewTokens, tokens.HasMore && !NextPageExceedsSkipLimit(page, limit), page, tokens.Total);
        }

        /// <summary>
        /// Get a projects default token
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens/default")]
        [ResponseType(typeof(ViewToken))]
        public async Task<IHttpActionResult> GetDefaultTokenAsync(string projectId) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var token = (await _repository.GetByTypeAndProjectIdAsync(TokenType.Access, projectId, new PagingOptions { Limit = 1 })).Documents.FirstOrDefault();
            if (token != null)
                return await OkModelAsync(token);

            return await PostAsync(new NewToken { OrganizationId = project.OrganizationId, ProjectId = projectId});
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the token.</param>
        /// <response code="404">The token could not be found.</response>
        [HttpGet]
        [Route("{id:token}", Name = "GetTokenById")]
        [ResponseType(typeof(ViewToken))]
        public override Task<IHttpActionResult> GetByIdAsync(string id) {
            return base.GetByIdAsync(id);
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
        [Route]
        [HttpPost]
        [ResponseType(typeof(ViewToken))]
        public override Task<IHttpActionResult> PostAsync(NewToken token) {
            return base.PostAsync(token);
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
        /// <response code="409">The token already exists.</response>
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens")]
        [HttpPost]
        [ResponseType(typeof(ViewToken))]
        public async Task<IHttpActionResult> PostByProjectAsync(string projectId, NewToken token) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return BadRequest();

            if (token == null)
                token = new NewToken();

            token.OrganizationId = project.OrganizationId;
            token.ProjectId = projectId;
            return await PostAsync(token);
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
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens")]
        [HttpPost]
        [ResponseType(typeof(ViewToken))]
        public async Task<IHttpActionResult> PostByOrganizationAsync(string organizationId, NewToken token) {
            if (token == null)
                token = new NewToken();

            if (!IsInOrganization(organizationId))
                return BadRequest();

            token.OrganizationId = organizationId;
            return await PostAsync(token);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of token identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more tokens were not found.</response>
        /// <response code="500">An error occurred while deleting one or more tokens.</response>
        [HttpDelete]
        [Route("{ids:tokens}")]
        public Task<IHttpActionResult> DeleteAsync(string ids) {
            return base.DeleteAsync(ids.FromDelimitedString());
        }

        #endregion

        protected override async Task<Token> GetModelAsync(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            var model = await _repository.GetByIdAsync(id, useCache);
            if (model == null)
                return null;

            if (!String.IsNullOrEmpty(model.OrganizationId) && !IsInOrganization(model.OrganizationId))
                return null;

            if (!String.IsNullOrEmpty(model.UserId) && model.UserId != ExceptionlessUser.Id)
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

            if (!String.IsNullOrEmpty(value.ProjectId) && !String.IsNullOrEmpty(value.UserId))
                return PermissionResult.DenyWithMessage("Token can't be associated to both user and project.");

            foreach (string scope in value.Scopes.ToList()) {
                if (scope != scope.ToLower()) {
                    value.Scopes.Remove(scope);
                    value.Scopes.Add(scope.ToLower());
                }

                if (!AuthorizationRoles.AllScopes.Contains(scope.ToLower()))
                    return PermissionResult.DenyWithMessage("Invalid token scope requested.");
            }

            if (value.Scopes.Count == 0)
                value.Scopes.Add(AuthorizationRoles.Client);

            if (value.Scopes.Contains(AuthorizationRoles.Client) && !User.IsInRole(AuthorizationRoles.User))
                return PermissionResult.Deny;

            if (value.Scopes.Contains(AuthorizationRoles.User) && !User.IsInRole(AuthorizationRoles.User) )
                return PermissionResult.Deny;

            if (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin) && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
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

            if (!String.IsNullOrEmpty(value.ApplicationId)) {
                var application = await _applicationRepository.GetByIdAsync(value.ApplicationId, true);
                if (application == null || !IsInOrganization(application.OrganizationId))
                    return PermissionResult.Deny;
            }

            return await base.CanAddAsync(value);
        }

        protected override Task<Token> AddModelAsync(Token value) {
            value.Id = StringExtensions.GetNewToken();
            value.CreatedUtc = value.ModifiedUtc = DateTime.UtcNow;
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
            if (!String.IsNullOrEmpty(value.ProjectId) && !await IsInProjectAsync(value.ProjectId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return await base.CanDeleteAsync(value);
        }

        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, useCache);
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
