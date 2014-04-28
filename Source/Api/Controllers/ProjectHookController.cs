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
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;

namespace Exceptionless.App.Controllers.API {
    [RoutePrefix(API_PREFIX + "projecthook")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectHookController : ApiController {
        private const string API_PREFIX = "api/v1/";

        private readonly IProjectHookRepository _projectHookRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;

        public ProjectHookController(ProjectHookRepository repository, IProjectRepository projectRepository, BillingManager billingManager) {
            _projectHookRepository = repository;
            _projectRepository = projectRepository;
            _billingManager = billingManager;
        }

        [Route]
        [HttpGet]
        public IHttpActionResult Get(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            ProjectHook entity = _projectHookRepository.GetByIdCached(id);
            if (!IsInProject(entity.ProjectId))
                return NotFound();

            return Ok(entity);
        }

        [Route]
        [HttpPost]
        public IHttpActionResult Post(ProjectHook value) {
            if (!IsInProject(value.ProjectId))
                return BadRequest();

            Project project = _projectRepository.GetByIdCached(value.ProjectId);
            if (!_billingManager.CanAddIntegration(project))
                return this.PlanLimitReached("Please upgrade your plan to add integrations.");

            return Ok(_projectHookRepository.Add(value));
        }

        //[Route]
        //[HttpPut]
        //public IHttpActionResult Put(string id, ProjectHook value) {
        //    ProjectHook original = GetEntity(id);
        //    if (original == null || !IsInProject(original.ProjectId))
        //        return BadRequest();

        //    return base.Put(id, value);
        //}

        //[Route]
        //[HttpPatch]
        //public IHttpActionResult Patch(string id, ProjectHook value) {
        //    ProjectHook original = GetEntity(id);
        //    if (original == null || !IsInProject(original.ProjectId))
        //        return BadRequest();

        //    return base.Patch(id, value);
        //}

        [Route]
        [HttpDelete]
        public IHttpActionResult Delete(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            ProjectHook original = _projectHookRepository.GetByIdCached(id);
            if (original == null || !IsInProject(original.ProjectId))
                return BadRequest();

            _projectHookRepository.Delete(original);
            return Ok();
        }

        [HttpGet]
        [Route("project/{projectId}")]
        public IHttpActionResult GetByProject(string projectId) {
            if (!IsInProject(projectId))
                return NotFound();

            return Ok(_projectHookRepository.GetByProjectId(projectId));
        }

        /// <summary>
        /// This controller action is called by zapier to create a hook subscription.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("subscribe")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Subscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();
            var eventType = data.GetValue("event").Value<string>();

            // TODO: Implement Subscribe.
            var project = Request.GetProject();
            if (project != null) {
                _projectHookRepository.Add(new ProjectHook {
                    EventTypes = new[] { eventType },
                    ProjectId = project.Id,
                    Url = targetUrl
                });
            }

            return StatusCode(HttpStatusCode.Created);
        }

        /// <summary>
        /// This controller action is called by zapier to remove a hook subscription.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        [Route("unsubscribe")]
        public IHttpActionResult Unsubscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();

            // don't let this anon method delete non-zapier hooks
            if (!targetUrl.Contains("zapier"))
                return NotFound();

            // TODO: Validate that a user owns this webhook.
            _projectHookRepository.Delete(Query.EQ(ProjectHookRepository.FieldNames.Url, targetUrl));

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to test auth.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [HttpPost]
        [Route("test")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Test() {
            return Ok(new[] {
                new { id = 1, Message = "Test message 1." },
                new { id = 2, Message = "Test message 2." }
            });
        }

        private bool IsInProject(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return false;

            Project project = _projectRepository.GetByIdCached(projectId);
            return Request.IsInOrganization(project.OrganizationId);
        }
    }
}