#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Models.Admin;

namespace Exceptionless.App.Controllers.API {
    [RoutePrefix(API_PREFIX + "/tokens")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class TokenController : RepositoryApiController<ITokenRepository, Token, Token, NewToken, Token> {
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
            var results = _repository.GetByTypeAndOrganizationId(TokenType.Access, organizationId, options).Select(Mapper.Map<Token, Token>).ToList();
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
            var results = _repository.GetByTypeAndProjectId(TokenType.Access, projectId, options).Select(Mapper.Map<Token, Token>).ToList();
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
                return Ok(Mapper.Map<Token, Token>(token));

            return Post(new NewToken { OrganizationId = project.OrganizationId, ProjectId = projectId});
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetTokenById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [Route]
        [HttpPost]
        public override IHttpActionResult Post(NewToken value) {
            return base.Post(value);
        }

        [HttpDelete]
        [Route("{ids:objectids}")]
        public override IHttpActionResult Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        #endregion

        protected override Token GetModel(string id, bool useCache = true) {
            var model = base.GetModel(id);
            return model != null && model.Type == TokenType.Access && IsInProject(model.ProjectId) ? model : null;
        }

        protected override PermissionResult CanAdd(Token value) {
            if (String.IsNullOrEmpty(value.OrganizationId))
                return PermissionResult.Deny;

            if (value.Scopes.Contains("admin") && !User.IsInRole(AuthorizationRoles.GlobalAdmin))
                return PermissionResult.Deny;

            Project project = _projectRepository.GetById(value.ProjectId, true);
            if (!IsInProject(project))
                return PermissionResult.Deny;

            if (!String.IsNullOrEmpty(value.ApplicationId)) {
                var application = _applicationRepository.GetById(value.ApplicationId, true);
                if (application == null || !IsInOrganization(application.OrganizationId))
                    return PermissionResult.Deny;
            }

            return base.CanAdd(value);
        }

        protected override Token AddModel(Token value) {
            value.Id = Guid.NewGuid().ToString("N");
            value.CreatedUtc = value.ModifiedUtc = DateTime.UtcNow;
            value.Type = TokenType.Access;
            value.UserId = User.GetUserId();

            return base.AddModel(value);
        }

        protected override PermissionResult CanDelete(Token value) {
            if (!IsInProject(value.ProjectId))
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