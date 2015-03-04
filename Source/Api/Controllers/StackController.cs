using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models.Admin;
using Exceptionless.Core.Models.Stats;
using Foundatio.Queues;
using Newtonsoft.Json.Linq;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stacks")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StackController : RepositoryApiController<IStackRepository, Stack, Stack, Stack, Stack> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly WebHookDataPluginManager _webHookDataPluginManager;
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly EventStats _eventStats;
        private readonly BillingManager _billingManager;
        private readonly FormattingPluginManager _formattingPluginManager;

        public StackController(IStackRepository stackRepository, IOrganizationRepository organizationRepository, 
            IProjectRepository projectRepository, IEventRepository eventRepository, IWebHookRepository webHookRepository, 
            WebHookDataPluginManager webHookDataPluginManager, IQueue<WebHookNotification> webHookNotificationQueue, 
            EventStats eventStats, BillingManager billingManager,
            FormattingPluginManager formattingPluginManager) : base(stackRepository) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
            _webHookRepository = webHookRepository;
            _webHookDataPluginManager = webHookDataPluginManager;
            _webHookNotificationQueue = webHookNotificationQueue;
            _eventStats = eventStats;
            _billingManager = billingManager;
            _formattingPluginManager = formattingPluginManager;

            AllowedFields.AddRange(new[] { "first", "last" });
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetStackById")]
        public IHttpActionResult GetById(string id, string offset = null) {
            var stack = GetModel(id);
            if (stack == null)
                return NotFound();

            return Ok(stack.ApplyOffset(GetOffset(offset)));
        }

        [HttpPost]
        [Route("{ids:objectids}/mark-fixed")]
        public IHttpActionResult MarkFixed([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => !s.DateFixed.HasValue).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks) {
                    // TODO: Implement Fixed in version.
                    stack.DateFixed = DateTime.UtcNow;
                    //stack.FixedInVersion = "GET CURRENT VERSION FROM ELASTIC SEARCH";
                    stack.IsRegressed = false;
                }

                _stackRepository.Save(stacks);
            }

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to mark the stack as fixed.
        /// </summary>
        [HttpPost]
        [Route("~/api/v1/stack/markfixed")]
        [Route("mark-fixed")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        public IHttpActionResult MarkFixed(JObject data) {
            string id = null;
            JToken value;
            if (data.TryGetValue("ErrorStack", out value))
                id = value.Value<string>();

            if (data.TryGetValue("Stack", out value))
                id = value.Value<string>();

            if (String.IsNullOrEmpty(id))
                return NotFound();

            if (id.StartsWith("http"))
                id = id.Substring(id.LastIndexOf('/') + 1);

            return MarkFixed(new [] { id });
        }

        [HttpPost]
        [Route("{id:objectid}/add-link")]
        public IHttpActionResult AddLink(string id, [NakedBody] string url) {
            var stack = GetModel(id, false);
            if (stack == null)
                return NotFound();

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
        [Route("~/api/v1/stack/addlink")]
        [Route("add-link")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        public IHttpActionResult AddLink(JObject data) {
            string id = null;
            JToken value;
            if (data.TryGetValue("ErrorStack", out value))
                id = value.Value<string>();
            
            if (data.TryGetValue("Stack", out value))
                id = value.Value<string>();

            if (String.IsNullOrEmpty(id))
                return NotFound();

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
                return NotFound();

            if (String.IsNullOrEmpty(url))
                return BadRequest();

            if (stack.References.Contains(url)) {
                stack.References.Remove(url);
                _stackRepository.Save(stack);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{ids:objectids}/mark-critical")]
        public IHttpActionResult MarkCritical([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => !s.OccurrencesAreCritical).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.OccurrencesAreCritical = true;

                _stackRepository.Save(stacks);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{ids:objectids}/mark-critical")]
        public IHttpActionResult MarkNotCritical([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => s.OccurrencesAreCritical).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.OccurrencesAreCritical = false;

                _stackRepository.Save(stacks);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{ids:objectids}/notifications")]
        public IHttpActionResult EnableNotifications([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => s.DisableNotifications).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.DisableNotifications = false;

                _stackRepository.Save(stacks);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{ids:objectids}/notifications")]
        public IHttpActionResult DisableNotifications([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => !s.DisableNotifications).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.DisableNotifications = true;

                _stackRepository.Save(stacks);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpDelete]
        [Route("{ids:objectids}/mark-fixed")]
        public IHttpActionResult MarkNotFixed([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => s.DateFixed.HasValue).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks) {
                    stack.DateFixed = null;
                    stack.IsRegressed = false;
                }

                _stackRepository.Save(stacks);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{ids:objectids}/mark-hidden")]
        public IHttpActionResult MarkHidden([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => !s.IsHidden).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.IsHidden = true;

                _stackRepository.Save(stacks);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{ids:objectids}/mark-hidden")]
        public IHttpActionResult MarkNotHidden([CommaDelimitedArray]string[] ids) {
            var stacks = GetModels(ids, false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => s.IsHidden).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.IsHidden = false;

                _stackRepository.Save(stacks);
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [Route("{id:objectid}/promote")]
        public IHttpActionResult Promote(string id) {
            if (String.IsNullOrEmpty(id))
                return NotFound();

            Stack stack = _stackRepository.GetById(id);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            if (!_billingManager.HasPremiumFeatures(stack.OrganizationId))
                return PlanLimitReached("Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.");

            List<WebHook> promotedProjectHooks = _webHookRepository.GetByProjectId(stack.ProjectId).Where(p => p.EventTypes.Contains(WebHookRepository.EventTypes.StackPromoted)).ToList();
            if (!promotedProjectHooks.Any())
                return NotImplemented("No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.");

            foreach (WebHook hook in promotedProjectHooks) {
                var context = new WebHookDataContext(hook.Version, stack, isNew: stack.TotalOccurrences == 1, isRegression: stack.IsRegressed);
                _webHookNotificationQueue.Enqueue(new WebHookNotification {
                    OrganizationId = hook.OrganizationId,
                    ProjectId = hook.ProjectId,
                    Url = hook.Url,
                    Data = _webHookDataPluginManager.CreateFromStack(context)
                });
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{ids:objectids}")]
        public override Task<IHttpActionResult> Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        protected override async Task DeleteModels(ICollection<Stack> values) {
            await _eventRepository.RemoveAllByStackIdsAsync(values.Select(s => s.Id).ToArray());
            await base.DeleteModels(values);
        }

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, sort, time, offset, mode, page, limit);
        }

        public IHttpActionResult GetInternal(string systemFilter, string userFilter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page + 1, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var validationResult = QueryProcessor.Process(userFilter);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, validationResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter), "last");

            var sortBy = GetSort(sort);
            var timeInfo = GetTimeInfo(time, offset);
            var options = new PagingOptions { Page = page, Limit = limit };
           
            List<Stack> stacks;
            try {
                stacks = _repository.GetByFilter(systemFilter, userFilter, sortBy.Item1, sortBy.Item2, timeInfo.Field, timeInfo.UtcRange.Start, timeInfo.UtcRange.End, options).Select(s => s.ApplyOffset(timeInfo.Offset)).ToList();
            } catch (ApplicationException ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().SetProperty("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Sort = sort, Time = time, Offset = offset, Page = page, Limit = limit }).AddTags("Search").Submit();
                return BadRequest("An error has occurred. Please check your search filter.");
            }

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(GetStackSummaries(stacks, timeInfo.Offset, timeInfo.UtcRange.UtcStart, timeInfo.UtcRange.UtcEnd), options.HasMore, page);

            return OkWithResourceLinks(stacks, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/stacks")]
        public IHttpActionResult GetByOrganization(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            return GetInternal(String.Concat("organization:", organizationId), filter, sort, time, offset, mode, page, limit);
        }

        [HttpGet]
        [Route("new")]
        public IHttpActionResult New(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, "-first", String.Concat("first|", time), offset, mode, page, limit);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/new")]
        public IHttpActionResult NewByProject(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, "-first", String.Concat("first|", time), offset, mode, page, limit);
        }

        [HttpGet]
        [Route("recent")]
        public IHttpActionResult Recent(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, "-last", String.Concat("last|", time), offset, mode, page, limit);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/recent")]
        public IHttpActionResult RecentByProject(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, "-last", String.Concat("last|", time), offset, mode, page, limit);
        }

        [HttpGet]
        [Route("frequent")]
        public IHttpActionResult Frequent(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return FrequentInternal(null, filter, time, offset, mode, page, limit);
        }

        public IHttpActionResult FrequentInternal(string systemFilter = null, string userFilter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var validationResult = QueryProcessor.Process(userFilter);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, validationResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter));
            
            var timeInfo = GetTimeInfo(time, offset);

            ICollection<TermStatsItem> terms;

            try {
                terms = _eventStats.GetTermsStats(timeInfo.UtcRange.Start, timeInfo.UtcRange.End, "stack_id", systemFilter, userFilter, timeInfo.Offset, GetSkip(page + 1, limit) + 1).Terms;
            } catch (ApplicationException ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().SetProperty("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Time = time, Offset = offset, Page = page, Limit = limit }).AddTags("Search").Submit();
                return BadRequest("An error has occurred. Please check your search filter.");
            }

            if (terms.Count == 0)
                return Ok(new object[0]);

            var stackIds = terms.Skip(skip).Take(limit + 1).Select(t => t.Term).ToArray();
            var stacks = _stackRepository.GetByIds(stackIds).Select(s => s.ApplyOffset(timeInfo.Offset)).ToList();

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase)) {
                var summaries = GetStackSummaries(stacks, terms);
                return OkWithResourceLinks(GetStackSummaries(stacks, terms).Take(limit).ToList(), summaries.Count > limit, page);
            }

            return OkWithResourceLinks(stacks.Take(limit).ToList(), stacks.Count > limit, page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/frequent")]
        public IHttpActionResult FrequentByProject(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return FrequentInternal(String.Concat("project:", projectId), filter, time, offset, mode, page, limit);
        }

        private ICollection<StackSummaryModel> GetStackSummaries(ICollection<Stack> stacks, TimeSpan offset, DateTime utcStart, DateTime utcEnd) {
            if (stacks.Count == 0)
                return new List<StackSummaryModel>();

            var terms = _eventStats.GetTermsStats(utcStart, utcEnd, "stack_id", String.Join(" OR ", stacks.Select(r => "stack:" + r.Id)), null, offset, stacks.Count).Terms;
            return GetStackSummaries(stacks, terms);
        }

        private ICollection<StackSummaryModel> GetStackSummaries(IEnumerable<Stack> stacks, IEnumerable<TermStatsItem> terms) {
            return stacks.Join(terms, s => s.Id, tk => tk.Term, (stack, term) => {
                var data = _formattingPluginManager.GetStackSummaryData(stack);
                var summary = new StackSummaryModel {
                    TemplateKey = data.TemplateKey,
                    Data = data.Data,
                    Id = stack.Id,
                    Title = stack.Title,
                    FirstOccurrence = term.FirstOccurrence,
                    LastOccurrence = term.LastOccurrence,
                    New = term.New,
                    Total = term.Total,
                    Unique = term.Unique,
                    Timeline = term.Timeline
                };

                return summary;
            }).ToList();
        }
    }
}