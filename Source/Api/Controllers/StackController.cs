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
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
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
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly EventStats _eventStats;
        private readonly BillingManager _billingManager;
        private readonly DataHelper _dataHelper;
        private readonly FormattingPluginManager _formattingPluginManager;

        public StackController(IStackRepository stackRepository, IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, IWebHookRepository webHookRepository, 
            WebHookDataPluginManager webHookDataPluginManager, IQueue<WebHookNotification> webHookNotificationQueue, 
            EventStats eventStats, BillingManager billingManager, DataHelper dataHelper,
            FormattingPluginManager formattingPluginManager) : base(stackRepository) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _webHookRepository = webHookRepository;
            _webHookDataPluginManager = webHookDataPluginManager;
            _webHookNotificationQueue = webHookNotificationQueue;
            _eventStats = eventStats;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
            _formattingPluginManager = formattingPluginManager;
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
        [Route("{id:objectid}/add-link")]
        public IHttpActionResult AddLink(string id, [NakedBody] string url) {
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

        [HttpPost]
        [Route("{id:objectid}/remove-link")]
        public IHttpActionResult RemoveLink(string id, [NakedBody] string url) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (String.IsNullOrEmpty(url))
                return BadRequest();

            if (stack.References.Contains(url)) {
                stack.References.Remove(url);
                _stackRepository.Save(stack);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id:objectid}/mark-critical")]
        public IHttpActionResult MarkCritical(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (!stack.OccurrencesAreCritical) {
                stack.OccurrencesAreCritical = true;
                _stackRepository.Save(stack);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/mark-critical")]
        public IHttpActionResult MarkNotCritical(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (stack.OccurrencesAreCritical) {
                stack.OccurrencesAreCritical = false;
                _stackRepository.Save(stack);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id:objectid}/notifications")]
        public IHttpActionResult EnableNotifications(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (stack.DisableNotifications) {
                stack.DisableNotifications = false;
                _stackRepository.Save(stack);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/notifications")]
        public IHttpActionResult DisableNotifications(string id) {
            var stack = GetModel(id, false);
            if (stack == null)
                return BadRequest();

            if (!stack.DisableNotifications) {
                stack.DisableNotifications = true;
                _stackRepository.Save(stack);
            }

            return StatusCode(HttpStatusCode.NoContent);
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
        public IHttpActionResult GetByOrganization(string organization = null, string before = null, string after = null, int limit = 10, string mode = null) {
            if (!String.IsNullOrEmpty(organization) && !CanAccessOrganization(organization))
                return NotFound();

            var organizationIds = new List<string>();
            if (!String.IsNullOrEmpty(organization) && CanAccessOrganization(organization))
                organizationIds.Add(organization);
            else
                organizationIds.AddRange(GetAssociatedOrganizationIds());

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByOrganizationIds(organizationIds, options).Select(e => e.ToProjectLocalTime(_projectRepository)).ToList();

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(results.Select(s => {
                    var summaryData = _formattingPluginManager.GetStackSummaryData(s);
                    return new StackSummaryModel {
                        TemplateKey = summaryData.TemplateKey,
                        Id = s.Id,
                        FirstOccurrence = s.FirstOccurrence,
                        LastOccurrence = s.LastOccurrence,
                        Data = summaryData.Data
                    };
                }).ToList(), options.HasMore, e => e.Id);

            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/new")]
        public IHttpActionResult New(string projectId, string before = null, string after = null, int limit = 10, DateTime? start = null, DateTime? end = null, string query = null, string mode = null) {
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
            var stacks = _stackRepository.GetNew(projectId, utcStart, utcEnd, options, query).ToList();
            var results = stacks.Where(m => m.FirstOccurrence >= retentionUtcCutoff).ToList();

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(results.Select(s => {
                    var summaryData = _formattingPluginManager.GetStackSummaryData(s);
                    return new StackSummaryModel {
                        TemplateKey = summaryData.TemplateKey,
                        Id = s.Id,
                        FirstOccurrence = s.FirstOccurrence,
                        LastOccurrence = s.LastOccurrence,
                        Data = summaryData.Data
                    };
                }).ToList(), options.HasMore, e => e.Id);

            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/recent")]
        public IHttpActionResult Recent(string projectId, string before = null, string after = null, int limit = 10, DateTime? start = null, DateTime? end = null, string query = null, string mode = null) {
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
            var stacks = _stackRepository.GetMostRecent(projectId, utcStart, utcEnd, options, query);
            var results = stacks.Where(es => es.LastOccurrence >= retentionUtcCutoff).ToList();

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(results.Select(s => {
                    var summaryData = _formattingPluginManager.GetStackSummaryData(s);
                    return new StackSummaryModel {
                        TemplateKey = summaryData.TemplateKey,
                        Id = s.Id,
                        FirstOccurrence = s.FirstOccurrence,
                        LastOccurrence = s.LastOccurrence,
                        Data = summaryData.Data
                    };
                }).ToList(), options.HasMore, e => e.Id);

            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/frequent")]
        public IHttpActionResult Frequent(string projectId, int page = 1, int limit = 10, DateTime? start = null, DateTime? end = null, string query = null, string mode = null) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            var range = GetDateRange(start, end);
            if (range.Item1 == range.Item2)
                return BadRequest("End date must be greater than start date.");

            if (page < 1)
                page = 1;

            if (limit < 0 || limit > 100)
                limit = 10;

            DateTime utcStart = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item1);
            DateTime utcEnd = _projectRepository.DefaultProjectLocalTimeToUtc(projectId, range.Item2);
            DateTime retentionUtcCutoff = _organizationRepository.GetById(project.OrganizationId, true).GetRetentionUtcCutoff();

            var terms = _eventStats.GetTermsStats(utcStart, utcEnd, "stack_id", "project:" + projectId, max: limit * page + 1).Terms;
            if (terms.Count == 0)
                return Ok(new object[0]);

            var stackIds = terms.Where(t => t.LastOccurrence >= retentionUtcCutoff).Skip((page - 1) * limit).Take(limit + 1).Select(t => t.Term).ToArray();
            var results = _stackRepository.GetByIds(stackIds);

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase)) {
                var summaries = terms.Join(results, tk => tk.Term, s => s.Id, (term, stack) => {
                    var data = _formattingPluginManager.GetStackSummaryData(stack);
                    var summary = new StackSummaryModel {
                        TemplateKey = data.TemplateKey,
                        Data = data.Data,
                        Id = stack.Id,
                        Title = stack.Title,
                        FirstOccurrence = stack.FirstOccurrence,
                        LastOccurrence = stack.LastOccurrence,

                        New = term.New,
                        Total = term.Total,
                        Unique = term.Unique,
                        Timeline = term.Timeline
                    };

                    return summary;
                }).ToList();

                return OkWithResourceLinks(summaries.Take(limit).ToList(), summaries.Count > limit, page);
            }

            return OkWithResourceLinks(results.Take(limit).ToList(), results.Count > limit, page);
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
                    esr.Type = s.Type;
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