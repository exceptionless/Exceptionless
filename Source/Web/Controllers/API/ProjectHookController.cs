#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Web.OData;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Web.Controllers.Service {
    [ExceptionlessAuthorize]
    public class ProjectHookController : RepositoryApiController<ProjectHook, IProjectHookRepository> {
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;

        public ProjectHookController(ProjectHookRepository repository, IProjectRepository projectRepository, BillingManager billingManager) : base(repository) {
            _projectRepository = projectRepository;
            _billingManager = billingManager;
        }

        [NonAction]
        public override IEnumerable<ProjectHook> Get() {
            throw new NotSupportedException();
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public IEnumerable<ProjectHook> GetByProject(string projectId) {
            if (!IsInProject(projectId))
                throw new HttpResponseException(NotFoundErrorResponseMessage(projectId));

            return _repository.GetByProjectId(projectId);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public override ProjectHook Get(string id) {
            ProjectHook entity = base.Get(id);

            if (!IsInProject(entity.ProjectId))
                throw new HttpResponseException(NotFoundErrorResponseMessage(entity.ProjectId));

            return entity;
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Post(ProjectHook value) {
            if (!IsInProject(value.ProjectId))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            Project project = _projectRepository.GetByIdCached(value.ProjectId);
            if (!_billingManager.CanAddIntegration(project))
                throw new HttpResponseException(PlanLimitReached("Please upgrade your plan to add integrations."));

            return base.Post(value);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Put(string id, Delta<ProjectHook> value) {
            ProjectHook original = GetEntity(id);
            if (original == null || !IsInProject(original.ProjectId))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            return base.Put(id, value);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Patch(string id, Delta<ProjectHook> value) {
            ProjectHook original = GetEntity(id);
            if (original == null || !IsInProject(original.ProjectId))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            return base.Patch(id, value);
        }

        [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Delete(string id) {
            ProjectHook original = GetEntity(id);
            if (original == null || !IsInProject(original.ProjectId))
                throw new HttpResponseException(BadRequestErrorResponseMessage());

            return base.Delete(id);
        }

        /// <summary>
        /// This controller action is called by zapier to create a hook subscription.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [ExceptionlessAuthorize(Roles = AuthorizationRoles.UserOrClient)]
        public HttpResponseMessage Subscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();
            var eventType = data.GetValue("event").Value<string>();

            if (User.Project != null) {
                _repository.Add(new ProjectHook {
                    EventTypes = new[] { eventType },
                    ProjectId = User.Project.Id,
                    Url = targetUrl
                });
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        }

        /// <summary>
        /// This controller action is called by zapier to remove a hook subscription.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public HttpResponseMessage Unsubscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();

            // don't let this anon method delete non-zapier hooks
            if (!targetUrl.Contains("zapier"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            _repository.Delete(Query.EQ(ProjectHookRepository.FieldNames.Url, targetUrl));

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// This controller action is called by zapier to test auth.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [HttpGet]
        [ExceptionlessAuthorize(Roles = AuthorizationRoles.UserOrClient)]
        public HttpResponseMessage Test() {
            return Request.CreateResponse(HttpStatusCode.OK, new[] {
                new { id = 1, Message = "Test message 1." },
                new { id = 2, Message = "Test message 2." }
            });
        }

        private bool IsInProject(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return false;

            Project project = _projectRepository.GetByIdCached(projectId);
            if (!User.IsInOrganization(project.OrganizationId))
                return false;

            return true;
        }
    }
}