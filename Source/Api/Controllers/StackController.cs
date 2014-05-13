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
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Exceptionless.Models.Stats;
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
        private readonly IMessagePublisher _messagePublisher;

        public StackController(IStackRepository stackRepository, IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, IProjectHookRepository projectHookRepository, 
            IQueue<WebHookNotification> webHookNotificationQueue, BillingManager billingManager, 
            DataHelper dataHelper, IMessagePublisher messagePublisher) : base(stackRepository) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _projectHookRepository = projectHookRepository;
            _webHookNotificationQueue = webHookNotificationQueue;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
            _messagePublisher = messagePublisher;
        }

        //TODO: Implement

        [Route]
        [HttpGet]
        public IEnumerable<Stack> Get() {
            return _stackRepository.GetByOrganizationIds(GetAssociatedOrganizationIds()).Take(100).ToList().Select(e => e.ToProjectLocalTime(_projectRepository));
        }

        [HttpGet]
        [Route("{id}")]
        public IHttpActionResult Get(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            Stack stack = _stackRepository.GetById(id, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return BadRequest();

            return Ok(stack.ToProjectLocalTime(_projectRepository));
        }

        //protected bool CanUpdateEntity(Stack original, Delta<Stack> value) {
        //    // TODO: Only let the client patch certain things.
        //    Stack entity = value.GetEntity();
        //    if (value.ContainsChangedProperty(t => t.FirstOccurrence) && original.FirstOccurrence != entity.FirstOccurrence)
        //        return false;

        //    if (value.ContainsChangedProperty(t => t.LastOccurrence) && original.LastOccurrence != entity.LastOccurrence)
        //        return false;

        //    if (value.ContainsChangedProperty(t => t.TotalOccurrences) && original.TotalOccurrences != entity.TotalOccurrences)
        //        return false;

        //    if (value.ContainsChangedProperty(t => t.ProjectId) && !String.Equals(original.ProjectId, entity.ProjectId, StringComparison.OrdinalIgnoreCase))
        //        return false;

        //    if (value.ContainsChangedProperty(t => t.OrganizationId) && !String.Equals(original.OrganizationId, entity.OrganizationId, StringComparison.OrdinalIgnoreCase))
        //        return false;

        //    return false;
        //}

        //protected Stack UpdateEntity(Stack original, Delta<Stack> value) {
        //    Stack entity = value.GetEntity();

        //    bool updateFixedInformation = (value.ContainsChangedProperty(t => t.DateFixed) && original.DateFixed != entity.DateFixed)
        //                                  || (value.ContainsChangedProperty(t => t.FixedInVersion) && original.FixedInVersion != entity.FixedInVersion);

        //    bool visibilityChanged = (value.ContainsChangedProperty(t => t.IsHidden) && original.IsHidden != entity.IsHidden);

        //    value.Patch(original);

        //    if (updateFixedInformation) {
        //        // TODO: Implement Fixed in version.
        //        if (original.DateFixed.HasValue) {
        //            original.DateFixed = DateTime.UtcNow;
        //            //original.FixedInVersion = "TODO";
        //            original.IsRegressed = false;
        //        } else {
        //            //original.DateFixed = null;
        //            //original.FixedInVersion = null;
        //        }
        //    }

        //    Stack stack = _stackRepository.Update(original);

        //    if (visibilityChanged)
        //        _stackRepository.InvalidateHiddenIdsCache(original.ProjectId);

        //    if (updateFixedInformation)
        //        _stackRepository.InvalidateFixedIdsCache(original.ProjectId);

        //    // notify client that the error stack has been updated.
        //    _notificationSender.StackUpdated(stack.OrganizationId, stack.ProjectId, stack.Id, stack.IsHidden, stack.IsFixed(), stack.Is404());

        //    return stack;
        //}

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
        /// This controller action is called by zapier to mark the stack as fixed.
        /// </summary>
        /// <param name="data"></param>
        [HttpPost]
        [OverrideAuthorization]
        [Route("{id}/mark-fixed")]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public HttpResponseMessage MarkFixed(JObject data) {
            var id = data.GetValue("ErrorStack").Value<string>();
            if (String.IsNullOrEmpty(id))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            Stack stack = _stackRepository.GetById(id);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            // TODO: Implement Fixed in version.
            stack.DateFixed = DateTime.UtcNow;
            //stack.FixedInVersion = "TODO";
            stack.IsRegressed = false;

            // TODO: Add a log entry.
            _stackRepository.Save(stack);

            _stackRepository.InvalidateFixedIdsCache(stack.ProjectId);

            // notify client that the error stack has been updated.
            _messagePublisher.PublishAsync(new StackUpdated { OrganizationId = stack.OrganizationId, ProjectId = stack.ProjectId, Id = stack.Id, IsHidden = stack.IsHidden, IsFixed = stack.IsFixed(), Is404 = stack.Is404() });

            return new HttpResponseMessage(HttpStatusCode.OK);
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

            // notify client that the error stack has been updated.
            _messagePublisher.PublishAsync(new StackUpdated { OrganizationId = stack.OrganizationId, ProjectId = stack.ProjectId, Id = stack.Id, IsHidden = stack.IsHidden, IsFixed = stack.IsFixed(), Is404 = stack.Is404() });

            return Ok();
        }

        [HttpGet]
        [Route("{projectId}/new")]
        public IHttpActionResult New(string projectId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();
            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2);

            pageSize = GetLimit(pageSize);
            int skip = GetSkip(page, pageSize);

            long count;
            List<Stack> query = _stackRepository.GetNew(projectId, utcStart, utcEnd, skip, pageSize, out count, hidden, @fixed, notfound).ToList();
            List<EventStackResult> models = query.Where(m => m.FirstOccurrence >= retentionUtcCutoff).Select(Mapper.Map<Stack, EventStackResult>).ToList();

            long totalLimitedByPlan = (query.Count - models.Count) > 0 ? count - (skip + models.Count) : 0;
            var result = new PlanPagedResult<EventStackResult>(models, totalLimitedByPlan: totalLimitedByPlan, totalCount: count) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            };

            // TODO: Only return the populated fields (currently all properties are being returned).
            return Ok(result);
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
            _messagePublisher.PublishAsync(new StackUpdated { OrganizationId = stack.OrganizationId, ProjectId = stack.ProjectId, Id = stack.Id, IsHidden = stack.IsHidden, IsFixed = stack.IsFixed(), Is404 = stack.Is404() });
        }
    }
}