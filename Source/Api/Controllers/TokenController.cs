using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
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
        
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens")]
        public IHttpActionResult GetByOrganization(string organizationId, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByTypeAndOrganizationId(TokenType.Access, organizationId, options).Select(Mapper.Map<Token, ViewToken>).ToList();
            return OkWithResourceLinks(results, options.HasMore, page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens")]
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

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens/default")]
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

        [HttpGet]
        [Route("{id:token}", Name = "GetTokenById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [Route]
        [HttpPost]
        public override IHttpActionResult Post(NewToken value) {
            return base.Post(value);
        }

        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens")]
        [HttpPost]
        public IHttpActionResult PostByProject(string projectId, NewToken value) {
            if (value == null)
                value = new NewToken();
            value.ProjectId = projectId;
            return base.Post(value);
        }

        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens")]
        [HttpPost]
        public IHttpActionResult PostByOrganization(string organizationId, NewToken value) {
            if (value == null)
                value = new NewToken();
            value.OrganizationId = organizationId;
            return base.Post(value);
        }

        [HttpDelete]
        [Route("{ids:tokens}")]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/tokens/{ids:tokens}")]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/tokens/{ids:tokens}")]
        public override Task<IHttpActionResult> Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
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
                Project project = _projectRepository.GetById(value.ProjectId, true);
                value.OrganizationId = project.OrganizationId;

                if (!IsInProject(project))
                    return PermissionResult.Deny;
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