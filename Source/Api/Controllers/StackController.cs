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
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Admin;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stacks")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StackController : RepositoryApiController<IStackRepository, Stack, Stack, Stack, Stack> {
        private readonly IStackRepository _stackRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly WebHookDataPluginManager _webHookDataPluginManager;
        private readonly EventStatsHelper _statsHelper;
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly BillingManager _billingManager;
        private readonly DataHelper _dataHelper;

        public StackController(IStackRepository stackRepository, IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, IWebHookRepository webHookRepository, 
            WebHookDataPluginManager webHookDataPluginManager, IQueue<WebHookNotification> webHookNotificationQueue, 
            EventStatsHelper statsHelper, BillingManager billingManager, DataHelper dataHelper) : base(stackRepository) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _webHookRepository = webHookRepository;
            _webHookDataPluginManager = webHookDataPluginManager;
            _webHookNotificationQueue = webHookNotificationQueue;
            _statsHelper = statsHelper;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
        }

        [HttpGet]
        [Route("{id:objectid}")]
        public override IHttpActionResult GetById(string id) {
            var stack = GetModel(id);
            if (stack == null)
                return NotFound();

            return Ok(stack.ToProjectLocalTime(_projectRepository));
        }

        [HttpPost]
        [Route("{id:objectid}/mark-fixed")]
        public IHttpActionResult MarkFixed(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (stack.DateFixed.HasValue)
                return Ok();

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
        [HttpPost]
        [Route("~/api/v{version:int=1}/stack/mark-fixed")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult MarkFixed(JObject data, int version = 1) {
            string id = null;
            if (version == 1)
                id = data.GetValue("ErrorStack").Value<string>();
            else if (version > 1)
                id = data.GetValue("Stack").Value<string>();

            if (String.IsNullOrEmpty(id))
                return BadRequest();

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            return MarkFixed(id);
        }

        // TODO: Add attribute validation for the url.
        [HttpPost]
        [Route("{id:objectid}/add-link/{url:minlength(3)}")]
        public IHttpActionResult AddLink(string id, string url) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (String.IsNullOrEmpty(url))
                return BadRequest();

            if (!stack.References.Contains(url)) {
                stack.References.Add(url);
                _stackRepository.Save(stack);
            }

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to add a reference link to a stack.
        /// </summary>
        [HttpPost]
        [Route("~/api/v{version:int=1}/stack/add-link")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult AddLink(JObject data, int version = 1) {
            string id = null;
            if (version == 1)
                id = data.GetValue("ErrorStack").Value<string>();
            else if (version > 1)
                id = data.GetValue("Stack").Value<string>();

            if (String.IsNullOrEmpty(id))
                return BadRequest();

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            var url = data.GetValue("Link").Value<string>();
            return AddLink(id, url);
        }

        [HttpDelete]
        [Route("{id:objectid}/mark-fixed")]
        public IHttpActionResult MarkNotFixed(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (!stack.DateFixed.HasValue)
                return Ok();

            stack.DateFixed = null;
            //stack.IsRegressed = false;

            // TODO: Add a log entry.
            _stackRepository.Save(stack);
            _stackRepository.InvalidateFixedIdsCache(stack.ProjectId);

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id:objectid}/mark-hidden")]
        public IHttpActionResult MarkHidden(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (!stack.IsHidden) {
                stack.IsHidden = true;
                _stackRepository.Save(stack);
                _stackRepository.InvalidateHiddenIdsCache(stack.ProjectId);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/mark-hidden")]
        public IHttpActionResult MarkNotHidden(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (stack.IsHidden) {
                stack.IsHidden = false;
                _stackRepository.Save(stack);
                _stackRepository.InvalidateHiddenIdsCache(stack.ProjectId);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id:objectid}/promote")]
        public IHttpActionResult Promote(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            Stack stack = _stackRepository.GetById(id);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return BadRequest();

            if (!_billingManager.HasPremiumFeatures(stack.OrganizationId))
                return PlanLimitReached("Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.");

            List<WebHook> promotedProjectHooks = _webHookRepository.GetByProjectId(stack.ProjectId).Where(p => p.EventTypes.Contains(WebHookRepository.EventTypes.StackPromoted)).ToList();
            if (!promotedProjectHooks.Any())
                return NotImplemented("No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.");

            foreach (WebHook hook in promotedProjectHooks) {
                var context = new WebHookDataContext(hook.Version, stack, isNew: stack.TotalOccurrences == 1, isRegression: stack.IsRegressed);
                _webHookNotificationQueue.EnqueueAsync(new WebHookNotification {
                    OrganizationId = hook.OrganizationId,
                    ProjectId = hook.ProjectId,
                    Url = hook.Url,
                    Data = _webHookDataPluginManager.CreateFromStack(context)
                });
                // TODO: Add stats metrics for webhooks.
            }

            return Ok();
        }

        [HttpGet]
        [Route]
        public IHttpActionResult GetByOrganization(string organization = null, string before = null, string after = null, int limit = 10) {
            if (!String.IsNullOrEmpty(organization) && !CanAccessOrganization(organization))
                return NotFound();

            var organizationIds = new List<string>();
            if (!String.IsNullOrEmpty(organization) && CanAccessOrganization(organization))
                organizationIds.Add(organization);
            else
                organizationIds.AddRange(GetAssociatedOrganizationIds());

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByOrganizationIds(organizationIds, options).Select(e => e.ToProjectLocalTime(_projectRepository)).ToList();
            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/new")]
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
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/recent")]
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
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/frequent")]
        public IHttpActionResult Frequent(string projectId, int page = 1, int limit = 10, DateTime? start = null, DateTime? end = null, bool hidden = false, bool @fixed = false, bool notfound = true) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            limit = GetLimit(limit);
            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();
            var frequent = _statsHelper.GetProjectErrorStats(projectId, _projectRepository.GetDefaultTimeOffset(projectId), start, end, retentionUtcCutoff, hidden, @fixed, notfound).MostFrequent;
            var results = frequent.Results.Skip(GetSkip(page, limit)).Take(limit).ToList();
            var stacks = _stackRepository.GetByIds(results.Select(s => s.Id).ToList());

            foreach (var esr in results) {
                var stack = stacks.SingleOrDefault(s => s.Id == esr.Id);
                if (stack == null) {
                    results.RemoveAll(r => r.Id == esr.Id);
                    continue;
                }

                // Stat's Id and Total properties are already calculated in the Results.
                esr.Type = stack.SignatureInfo.ContainsKey("ExceptionType") ? stack.SignatureInfo["ExceptionType"] : null;
                esr.Method = stack.SignatureInfo.ContainsKey("Method") ? stack.SignatureInfo["Method"] : null;
                esr.Path = stack.SignatureInfo.ContainsKey("Path") ? stack.SignatureInfo["Path"] : null;
                esr.Is404 = stack.SignatureInfo.ContainsKey("Path");

                esr.Title = stack.Title;
                esr.First = stack.FirstOccurrence;
                esr.Last = stack.LastOccurrence;
            }

            Dictionary<string, IEnumerable<string>> header = null;
            if (frequent.Results.Count != limit && frequent.TotalLimitedByPlan.HasValue)
                header = GetLimitedByPlanHeader(frequent.TotalLimitedByPlan.Value);

            return OkWithResourceLinks(results, frequent.Results.Count > (GetSkip(page, limit) + limit), e => e.Id, header);
        }

        [HttpGet]
        [Route("{id:objectid}/reset-data")]
        public async Task<IHttpActionResult> ResetDataAsync(string id) {
            if (String.IsNullOrEmpty(id))
                return NotFound();

            Stack stack = _stackRepository.GetById(id, true);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            await _dataHelper.ResetStackDataAsync(id);
            return Ok();
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