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
using Exceptionless.Core.Models.Admin;

namespace Exceptionless.App.Controllers.API {
    [RoutePrefix(API_PREFIX + "/tokens")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class TokenController : RepositoryApiController<ITokenRepository, Token, ViewToken, NewToken, Token> {
        private readonly IApplicationRepository _applicationRepository;
        private readonly IProjectRepository _projectRepository;

        public TokenController(ITokenRepository repository, IApplicationRepository applicationRepository, IProjectRepository projectRepository) : base(repository) {
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
        public IHttpActionResult GetByOrganization(string organizationId, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByTypeAndOrganizationId(TokenType.Access, organizationId, options).Select(Mapper.Map<Token, ViewToken>).ToList();
            return OkWithResourceLinks(results, options.HasMore, page);
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
        public IHttpActionResult GetByProject(string projectId, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByTypeAndProjectId(TokenType.Access, projectId, options).Select(Mapper.Map<Token, ViewToken>).ToList();
            return OkWithResourceLinks(results, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        /// <summary>
        /// Get a projects default token
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens/default")]
        [ResponseType(typeof(ViewToken))]
        public IHttpActionResult GetDefaultToken(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var token = _repository.GetByTypeAndProjectId(TokenType.Access, projectId, new PagingOptions { Limit = 1 }).FirstOrDefault();
            if (token != null)
                return Ok(Mapper.Map<Token, ViewToken>(token));

            return Post(new NewToken { OrganizationId = project.OrganizationId, ProjectId = projectId});
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the token.</param>
        /// <response code="404">The token could not be found.</response>
        [HttpGet]
        [Route("{id:token}", Name = "GetTokenById")]
        [ResponseType(typeof(ViewToken))]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
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
        public override IHttpActionResult Post(NewToken token) {
            return base.Post(token);
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
        public IHttpActionResult PostByProject(string projectId, NewToken token) {
            if (token == null)
                token = new NewToken();

            var project = _projectRepository.GetById(projectId, true);
            if (!IsInProject(project))
                return BadRequest();

            token.OrganizationId = project.OrganizationId;
            token.ProjectId = projectId;
            return Post(token);
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
        public IHttpActionResult PostByOrganization(string organizationId, NewToken token) {
            if (token == null)
                token = new NewToken();

            if (!IsInOrganization(organizationId))
                return BadRequest();

            token.OrganizationId = organizationId;
            return Post(token);
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
        public async Task<IHttpActionResult> DeleteAsync(string ids) {
            return await base.DeleteAsync(ids.FromDelimitedString());
        }

        #endregion

        protected override Token GetModel(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            var model = _repository.GetById(id, useCache);
            if (model == null)
                return null;

            if (!String.IsNullOrEmpty(model.OrganizationId) && !IsInOrganization(model.OrganizationId))
                return null;

            if (!String.IsNullOrEmpty(model.UserId) && model.UserId != Request.GetUser().Id)
                return null;

            if (model.Type != TokenType.Access)
                return null;

            if (!String.IsNullOrEmpty(model.ProjectId) && !IsInProject(model.ProjectId))
                return null;

            return model;
        }

        protected override PermissionResult CanAdd(Token value) {
            if (String.IsNullOrEmpty(value.OrganizationId))
                return PermissionResult.Deny;

            if ((!String.IsNullOrEmpty(value.ProjectId) || !String.IsNullOrEmpty(value.DefaultProjectId)) && !String.IsNullOrEmpty(value.UserId))
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
                Project project = _projectRepository.GetById(value.ProjectId, true);
                if (!IsInProject(project))
                    return PermissionResult.Deny;

                value.OrganizationId = project.OrganizationId;
                value.DefaultProjectId = null;
            }

            if (!String.IsNullOrEmpty(value.DefaultProjectId)) {
                Project project = _projectRepository.GetById(value.DefaultProjectId, true);
                if (!IsInProject(project))
                    return PermissionResult.Deny;

                value.OrganizationId = project.OrganizationId;
            }

            if (!String.IsNullOrEmpty(value.ApplicationId)) {
                var application = _applicationRepository.GetById(value.ApplicationId, true);
                if (application == null || !IsInOrganization(application.OrganizationId))
                    return PermissionResult.Deny;
            }

            return base.CanAdd(value);
        }

        protected override Token AddModel(Token value) {
            value.Id = StringExtensions.GetNewToken();
            value.CreatedUtc = value.ModifiedUtc = DateTime.UtcNow;
            value.Type = TokenType.Access;
            value.CreatedBy = Request.GetUser().Id;

            // add implied scopes
            if (value.Scopes.Contains(AuthorizationRoles.GlobalAdmin))
                value.Scopes.Add(AuthorizationRoles.User);

            if (value.Scopes.Contains(AuthorizationRoles.User))
                value.Scopes.Add(AuthorizationRoles.Client);

            return base.AddModel(value);
        }

        protected override PermissionResult CanDelete(Token value) {
            if (!String.IsNullOrEmpty(value.ProjectId) && !IsInProject(value.ProjectId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return base.CanDelete(value);
        }

        private bool IsInProject(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return false;

            return IsInProject(_projectRepository.GetById(projectId, true));
        }

        private bool IsInProject(Project value) {
            if (value == null)
                return false;

            return IsInOrganization(value.OrganizationId);
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<NewToken, Token>() == null)
                Mapper.CreateMap<NewToken, Token>().ForMember(m => m.Type, m => m.Ignore());

            base.CreateMaps();
        }
    }
}