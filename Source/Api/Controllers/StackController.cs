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
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Api.Controllers {
    [ConfigurationResponseFilter]
    [RoutePrefix(API_PREFIX + "stack")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StackController : RepositoryApiController<IStackRepository, Stack, Stack, Stack, Stack> {
        private readonly IStackRepository _stackRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IProjectHookRepository _projectHookRepository;
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly BillingManager _billingManager;
        private readonly DataHelper _dataHelper;

        public StackController(IStackRepository stackRepository, IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, IProjectHookRepository projectHookRepository, 
            IQueue<WebHookNotification> webHookNotificationQueue, BillingManager billingManager, 
            DataHelper dataHelper) : base(stackRepository) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _projectHookRepository = projectHookRepository;
            _webHookNotificationQueue = webHookNotificationQueue;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
        }

        #region CRUD

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string organizationId = null, string before = null, string after = null, int limit = 10) {
            if (!CanAccessOrganization(organizationId))
                return NotFound();

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByOrganizationId(organizationId, options).Select(e => e.ToProjectLocalTime(_projectRepository)).ToList();
            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("{id}")]
        public override IHttpActionResult GetById(string id) {
            var stack = GetModel(id);
            if (stack == null)
                return NotFound();

            return Ok(stack.ToProjectLocalTime(_projectRepository));
        }

        #endregion

        [HttpPost]
        [Route("{id}/mark-fixed")]
        public IHttpActionResult MarkFixed(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            // TODO: Implement Fixed in version.
            stack.DateFixed = DateTime.UtcNow;
            //stack.FixedInVersion = "TODO";
            stack.IsRegressed = false;

            // TODO: Add a log entry.
            _stackRepository.Save(stack);
            _stackRepository.InvalidateFixedIdsCache(stack.ProjectId);

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to mark the stack as fixed.
        /// </summary>
        /// <param name="data"></param>
        [HttpPost]
        [Route("{id}/mark-fixed")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult MarkFixed(JObject data) {
            var id = data.GetValue("ErrorStack").Value<string>();
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            return MarkFixed(id);
        }

        [HttpDelete]
        [Route("{id}/mark-fixed")]
        public IHttpActionResult MarkNotFixed(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            stack.DateFixed = null;
            //stack.IsRegressed = false;

            // TODO: Add a log entry.
            _stackRepository.Save(stack);
            _stackRepository.InvalidateFixedIdsCache(stack.ProjectId);

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id}/mark-hidden")]
        public IHttpActionResult MarkHidden(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            stack.IsHidden = true;
            _stackRepository.Save(stack);
            _stackRepository.InvalidateHiddenIdsCache(stack.ProjectId);

            return Ok();
        }

        [HttpDelete]
        [Route("{id}/mark-hidden")]
        public IHttpActionResult MarkNotHidden(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            stack.IsHidden = false;
            _stackRepository.Save(stack);
            _stackRepository.InvalidateHiddenIdsCache(stack.ProjectId);

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id}/promote")]
        public IHttpActionResult Promote(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            Stack stack = _stackRepository.GetById(id);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return BadRequest();

            if (!_billingManager.HasPremiumFeatures(stack.OrganizationId))
                return PlanLimitReached("Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.");

            List<ProjectHook> promotedProjectHooks = _projectHookRepository.GetByProjectId(stack.ProjectId).Where(p => p.EventTypes.Contains(ProjectHookRepository.EventTypes.StackPromoted)).ToList();
            if (!promotedProjectHooks.Any())
                return NotImplemented("No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.");

            foreach (ProjectHook hook in promotedProjectHooks) {
                _webHookNotificationQueue.EnqueueAsync(new WebHookNotification {
                    ProjectId = hook.ProjectId,
                    Url = hook.Url,
                    Data = WebHookStack.FromStack(stack, _projectRepository, _organizationRepository)
                });
            }

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to add a reference link to a stack.
        /// </summary>
        /// <param name="data"></param>
        [HttpPost]
        [OverrideAuthorization]
        [Route("add-link")]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult AddLink(JObject data) {
            var id = data.GetValue("ErrorStack").Value<string>();
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            Stack stack = _stackRepository.GetById(id);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return BadRequest();

            var url = data.GetValue("Link").Value<string>();
            if (String.IsNullOrEmpty(url))
                return BadRequest();

            if (!stack.References.Contains(url)) {
                stack.References.Add(url);
                _stackRepository.Save(stack);
            }

            return Ok();
        }

        [HttpGet]
        [Route("project/{projectId}/new")]
        public IHttpActionResult New(string projectId, string before = null, string after = null, int limit = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2);
            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();

            var options = new PagingOptions().WithBefore(before).WithAfter(after).WithLimit(limit);
            var stacks = _stackRepository.GetNew(projectId, utcStart, utcEnd, options, hidden, @fixed, notfound).ToList();
            List<EventStackResult> results = stacks.Where(m => m.FirstOccurrence >= retentionUtcCutoff).Select(Mapper.Map<Stack, EventStackResult>).ToList();

            return OkWithResourceLinks(results, options.HasMore, e => e.First.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"), GetLimitedByPlanHeader(stacks.Count - results.Count));
        }

        [HttpGet]
        [Route("project/{projectId}/recent")]
        public IHttpActionResult Recent(string projectId, string before = null, string after = null, int limit = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2);
            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();

            var paging = new PagingOptions().WithBefore(before).WithAfter(after).WithLimit(limit);
            var results = _stackRepository.GetMostRecent(projectId, utcStart, utcEnd, paging, hidden, @fixed, notfound);
            var stacks = results.Where(es => es.LastOccurrence >= retentionUtcCutoff).Select(Mapper.Map<Stack,EventStackResult>).ToList();

            return OkWithResourceLinks(results, paging.HasMore, e => e.Id, GetLimitedByPlanHeader(stacks.Count - results.Count));
        }

        [HttpGet]
        [Route("{id}/reset-data")]
        public void ResetData(string id) {
            if (String.IsNullOrEmpty(id))
                return;

            Stack stack = _stackRepository.GetById(id, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return;

            _dataHelper.ResetStackData(id);
        }

        protected override void CreateMaps() {
            if (Mapper.FindTypeMapFor<Stack, EventStackResult>() == null)
                Mapper.CreateMap<Stack, EventStackResult>().AfterMap((s, esr) => {
                    esr.Id = s.Id;
                    esr.Type = s.SignatureInfo.ContainsKey("ExceptionType") ? s.SignatureInfo["ExceptionType"] : null;
                    esr.Method = s.SignatureInfo.ContainsKey("Method") ? s.SignatureInfo["Method"] : null;
                    esr.Path = s.SignatureInfo.ContainsKey("Path") ? s.SignatureInfo["Path"] : null;
                    esr.Is404 = s.SignatureInfo.ContainsKey("Path");
                    esr.Title = s.Title;
                    esr.Total = s.TotalOccurrences;
                    esr.First = s.FirstOccurrence;
                    esr.Last = s.LastOccurrence;
                });
        }
    }
}