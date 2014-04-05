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
using Exceptionless.App.Hubs;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Core.Web.OData;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Exceptionless.Models.Stats;
using Newtonsoft.Json.Linq;
using ServiceStack.Messaging;

namespace Exceptionless.App.Controllers.API {
    [ConfigurationResponseFilter]
    [ExceptionlessAuthorize(Roles = AuthorizationRoles.User)]
    public class StackController : RepositoryOwnedByOrganizationApiController<Stack, IStackRepository> {
        private readonly IMessageFactory _messageFactory;
        private readonly BillingManager _billingManager;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IProjectHookRepository _projectHookRepository;
        private readonly NotificationSender _notificationSender;
        private readonly DataHelper _dataHelper;

        public StackController(IStackRepository repository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
            IProjectHookRepository projectHookRepository, IMessageFactory messageFactory, BillingManager billingManager, NotificationSender notificationSender, DataHelper dataHelper)
            : base(repository) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _projectHookRepository = projectHookRepository;
            _messageFactory = messageFactory;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _dataHelper = dataHelper;
        }

        public override IEnumerable<Stack> Get() {
            return base.Get().Select(e => e.ToProjectLocalTime(_projectRepository));
        }

        public override Stack Get(string id) {
            return base.Get(id).ToProjectLocalTime(_projectRepository);
        }

        [NonAction]
        public override HttpResponseMessage Put(string id, Delta<Stack> value) {
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        [NonAction]
        public override HttpResponseMessage Post(Stack value) {
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        [NonAction]
        public override HttpResponseMessage Delete(string id) {
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        protected override bool CanUpdateEntity(Stack original, Delta<Stack> value) {
            // TODO: Only let the client patch certain things.
            Stack entity = value.GetEntity();
            if (value.ContainsChangedProperty(t => t.FirstOccurrence) && original.FirstOccurrence != entity.FirstOccurrence)
                return false;

            if (value.ContainsChangedProperty(t => t.LastOccurrence) && original.LastOccurrence != entity.LastOccurrence)
                return false;

            if (value.ContainsChangedProperty(t => t.TotalOccurrences) && original.TotalOccurrences != entity.TotalOccurrences)
                return false;

            if (value.ContainsChangedProperty(t => t.ProjectId) && !String.Equals(original.ProjectId, entity.ProjectId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (value.ContainsChangedProperty(t => t.OrganizationId) && !String.Equals(original.OrganizationId, entity.OrganizationId, StringComparison.OrdinalIgnoreCase))
                return false;

            return base.CanUpdateEntity(original, value);
        }

        protected override Stack UpdateEntity(Stack original, Delta<Stack> value) {
            Stack entity = value.GetEntity();

            bool updateFixedInformation = (value.ContainsChangedProperty(t => t.DateFixed) && original.DateFixed != entity.DateFixed)
                                          || (value.ContainsChangedProperty(t => t.FixedInVersion) && original.FixedInVersion != entity.FixedInVersion);

            bool visibilityChanged = (value.ContainsChangedProperty(t => t.IsHidden) && original.IsHidden != entity.IsHidden);

            value.Patch(original);

            if (updateFixedInformation) {
                // TODO: Implement Fixed in version.
                if (original.DateFixed.HasValue) {
                    original.DateFixed = DateTime.UtcNow;
                    //original.FixedInVersion = "TODO";
                    original.IsRegressed = false;
                } else {
                    //original.DateFixed = null;
                    //original.FixedInVersion = null;
                }
            }

            Stack stack = _repository.Update(original);

            if (visibilityChanged)
                _repository.InvalidateHiddenIdsCache(original.ProjectId);

            if (updateFixedInformation)
                _repository.InvalidateFixedIdsCache(original.ProjectId);

            // notify client that the error stack has been updated.
            _notificationSender.StackUpdated(stack.OrganizationId, stack.ProjectId, stack.Id, stack.IsHidden, stack.IsFixed(), stack.Is404());

            return stack;
        }

        [HttpPost]
        public void Promote(string id) {
            Stack entity = GetEntity(id);
            if (entity == null)
                throw new HttpResponseException(base.NotFoundErrorResponseMessage(id));

            if (!_billingManager.HasPremiumFeatures(entity.OrganizationId))
                throw new HttpResponseException(PlanLimitReached("Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature."));

            List<ProjectHook> promotedProjectHooks = _projectHookRepository.GetByProjectId(entity.ProjectId).Where(p => p.EventTypes.Contains(ProjectHookRepository.EventTypes.StackPromoted)).ToList();
            if (!promotedProjectHooks.Any()) {
                throw new HttpResponseException(Request != null
                    ? Request.CreateErrorResponse(HttpStatusCode.NotImplemented, "No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.")
                    : new HttpResponseMessage(HttpStatusCode.NotImplemented));
            }

            using (IMessageProducer messageProducer = _messageFactory.CreateMessageProducer()) {
                foreach (ProjectHook hook in promotedProjectHooks) {
                    messageProducer.Publish(new WebHookNotification {
                        ProjectId = hook.ProjectId,
                        Url = hook.Url,
                        Data = WebHookStack.FromStack(entity, _projectRepository, _organizationRepository)
                    });
                }
            }
        }

        /// <summary>
        /// This controller action is called by zapier to mark the stack as fixed.
        /// </summary>
        /// <param name="data"></param>
        [HttpPost]
        [ExceptionlessAuthorize(Roles = AuthorizationRoles.UserOrClient)]
        public HttpResponseMessage MarkFixed(JObject data) {
            var id = data.GetValue("ErrorStack").Value<string>();
            if (String.IsNullOrEmpty(id))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            Stack stack = _repository.GetById(id);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            // TODO: Implement Fixed in version.
            stack.DateFixed = DateTime.UtcNow;
            //stack.FixedInVersion = "TODO";
            stack.IsRegressed = false;

            // TODO: Add a log entry.
            _repository.Update(stack);

            _repository.InvalidateFixedIdsCache(stack.ProjectId);

            // notify client that the error stack has been updated.
            _notificationSender.StackUpdated(stack.OrganizationId, stack.ProjectId, stack.Id, stack.IsHidden, stack.IsFixed(), stack.Is404());

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// This controller action is called by zapier to add a reference link to a stack.
        /// </summary>
        /// <param name="data"></param>
        [HttpPost]
        [ExceptionlessAuthorize(Roles = AuthorizationRoles.UserOrClient)]
        public HttpResponseMessage AddLink(JObject data) {
            var id = data.GetValue("ErrorStack").Value<string>();
            if (String.IsNullOrEmpty(id))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            Stack stack = _repository.GetById(id);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            var url = data.GetValue("Link").Value<string>();
            if (String.IsNullOrEmpty(url))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            if (!stack.References.Contains(url)) {
                stack.References.Add(url);
                _repository.Update(stack);
            }

            // notify client that the error stack has been updated.
            _notificationSender.StackUpdated(stack.OrganizationId, stack.ProjectId, stack.Id, stack.IsHidden, stack.IsFixed(), stack.Is404());

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpGet]
        public IHttpActionResult New(string projectId, int page = 1, int pageSize = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetByIdCached(projectId);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            DateTime retentionUtcCutoff = _organizationRepository.GetByIdCached(project.OrganizationId).GetRetentionUtcCutoff();
            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2);

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            long count;
            List<Stack> query = _repository.GetNew(projectId, utcStart, utcEnd, skip, pageSize, out count, hidden, @fixed, notfound).ToList();
            List<ErrorStackResult> models = query.Where(m => m.FirstOccurrence >= retentionUtcCutoff).Select(Mapper.Map<Stack, ErrorStackResult>).ToList();

            long totalLimitedByPlan = (query.Count - models.Count) > 0 ? count - (skip + models.Count) : 0;
            var result = new PlanPagedResult<ErrorStackResult>(models, totalLimitedByPlan: totalLimitedByPlan, totalCount: count) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            };

            // TODO: Only return the populated fields (currently all properties are being returned).
            return Ok(result);
        }

        [HttpGet]
        public void ResetData(string id) {
            if (String.IsNullOrEmpty(id))
                return;

            Stack stack = _repository.GetByIdCached(id);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return;

            _dataHelper.ResetStackData(id);
            _notificationSender.StackUpdated(stack.OrganizationId, stack.ProjectId, stack.Id, stack.IsHidden, stack.IsFixed(), stack.Is404());
        }
    }
}