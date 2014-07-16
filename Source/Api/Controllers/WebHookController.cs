#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net;
using System.Web.Http;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Newtonsoft.Json.Linq;

namespace Exceptionless.App.Controllers.API {
    [RoutePrefix(API_PREFIX + "/webhooks")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class WebHookController : RepositoryApiController<IWebHookRepository, WebHook, WebHook, NewWebHook, NewWebHook> {
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;

        public WebHookController(IWebHookRepository repository, IProjectRepository projectRepository, BillingManager billingManager) : base(repository) {
            _projectRepository = projectRepository;
            _billingManager = billingManager;
        }

        #region CRUD
        
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/webhooks")]
        public IHttpActionResult GetByProject(string projectId, string before = null, string after = null, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByProjectId(projectId, options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetWebHookById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [Route]
        [HttpPost]
        public override IHttpActionResult Post(NewWebHook value) {
            return base.Post(value);
        }

        [HttpPut]
        [HttpPatch]
        [Route("{id:objectid}")]
        public override IHttpActionResult Patch(string id, Delta<NewWebHook> changes) {
            return base.Patch(id, changes);
        }

        [HttpDelete]
        [Route("{id:objectid}")]
        public override IHttpActionResult Delete(string id) {
            return base.Delete(id);
        }

        #endregion

        /// <summary>
        /// This controller action is called by zapier to create a hook subscription.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("subscribe")]
        [Route("~/api/v1/projecthook/subscribe")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Subscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();
            var eventType = data.GetValue("event").Value<string>();

            if (User.GetProjectId() != null) {
                _repository.Add(new WebHook {
                    EventTypes = new[] { eventType },
                    ProjectId = User.GetProjectId(),
                    Url = targetUrl
                });
            } else {
                _repository.Add(new WebHook {
                    EventTypes = new[] { eventType },
                    OrganizationId = Request.GetDefaultOrganizationId(),
                    Url = targetUrl
                });
            }

            return StatusCode(HttpStatusCode.Created);
        }

        /// <summary>
        /// This controller action is called by zapier to remove a hook subscription.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [Route("unsubscribe")]
        [Route("~/api/v1/projecthook/unsubscribe")]
        public IHttpActionResult Unsubscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();

            // don't let this anon method delete non-zapier hooks
            if (!targetUrl.Contains("zapier"))
                return NotFound();

            // TODO: Validate that a user owns this webhook.
            _repository.RemoveByUrl(targetUrl);

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to test auth.
        /// </summary>
        [HttpGet]
        [HttpPost]
        [Route("test")]
        [Route("~/api/v1/projecthook/test")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Test() {
            return Ok(new[] {
                new { id = 1, Message = "Test message 1." },
                new { id = 2, Message = "Test message 2." }
            });
        }

        protected override WebHook GetModel(string id, bool useCache = true) {
            var model = base.GetModel(id);
            return model != null && IsInProject(model.ProjectId) ? model : null;
        }

        protected override PermissionResult CanAdd(WebHook value) {
            if (String.IsNullOrEmpty(value.ProjectId))
                return PermissionResult.DenyWithResult(BadRequest());

            Project project = _projectRepository.GetById(value.ProjectId, true);
            if (!IsInProject(project))
                return PermissionResult.DenyWithResult(BadRequest());

            if (!_billingManager.CanAddIntegration(project))
                return PermissionResult.DenyWithResult(PlanLimitReached("Please upgrade your plan to add integrations."));

            return base.CanAdd(value);
        }

        protected override PermissionResult CanUpdate(WebHook original, Delta<WebHook> changes) {
            if (!IsInProject(original.ProjectId))
                return PermissionResult.DenyWithResult(BadRequest());
            
            return base.CanUpdate(original, changes);
        }

        protected override PermissionResult CanDelete(WebHook value) {
            if (!IsInProject(value.ProjectId))
                return PermissionResult.DenyWithResult(BadRequest());

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
    }
}