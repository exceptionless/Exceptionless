using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
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

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the stack.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the `time` filter. This is used for time zone support.</param>
        /// <response code="404">The stack could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}", Name = "GetStackById")]
        [ResponseType(typeof(Stack))]
        public IHttpActionResult GetById(string id, string offset = null) {
            var stack = GetModel(id);
            if (stack == null)
                return NotFound();

            return Ok(stack.ApplyOffset(GetOffset(offset)));
        }

        /// <summary>
        /// Mark fixed
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/mark-fixed")]
        public IHttpActionResult MarkFixed(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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
        [ApiExplorerSettings(IgnoreApi = true)]
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

            return MarkFixed(id);
        }

        /// <summary>
        /// Add reference link
        /// </summary>
        /// <param name="id">The identifier of the stack.</param>
        /// <param name="url">The reference link.</param>
        /// <response code="400">Invalid reference link.</response>
        /// <response code="404">The stack could not be found.</response>
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
        [ApiExplorerSettings(IgnoreApi = true)]
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

        /// <summary>
        /// Remove reference link
        /// </summary>
        /// <param name="id">The identifier of the stack.</param>
        /// <param name="url">The reference link.</param>
        /// <response code="204">The reference link was removed.</response>
        /// <response code="400">Invalid reference link.</response>
        /// <response code="404">The stack could not be found.</response>
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

        /// <summary>
        /// Mark future occurrences as critical
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/mark-critical")]
        public IHttpActionResult MarkCritical(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Mark future occurrences as not critical
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="204">The stacks were marked as not critical.</response>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpDelete]
        [Route("{ids:objectids}/mark-critical")]
        public IHttpActionResult MarkNotCritical(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Enable notifications
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/notifications")]
        public IHttpActionResult EnableNotifications(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Disable notifications
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="204">Notifications are disabled for the stacks.</response>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpDelete]
        [Route("{ids:objectids}/notifications")]
        public IHttpActionResult DisableNotifications(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Mark not fixed
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="204">The stacks were marked as not fixed.</response>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpDelete]
        [Route("{ids:objectids}/mark-fixed")]
        public IHttpActionResult MarkNotFixed(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Mark hidden
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/mark-hidden")]
        public IHttpActionResult MarkHidden(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Mark not hidden
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="204">The stacks were marked as not hidden.</response>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpDelete]
        [Route("{ids:objectids}/mark-hidden")]
        public IHttpActionResult MarkNotHidden(string ids) {
            var stacks = GetModels(ids.FromDelimitedString(), false);
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

        /// <summary>
        /// Promote to external service
        /// </summary>
        /// <param name="id">The identifier of the stack.</param>
        /// <response code="404">The stack could not be found.</response>
        /// <response code="426">Promote to External is a premium feature used to promote an error stack to an external system.</response>
        /// <response code="501">"No promoted web hooks are configured for this project.</response>
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

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more stacks were not found.</response>
        /// <response code="500">An error occurred while deleting one or more stacks.</response>
        [HttpDelete]
        [Route("{ids:objectids}")]
        public async Task<IHttpActionResult> DeleteAsync(string ids) {
            return await base.DeleteAsync(ids.FromDelimitedString());
        }

        protected override async Task DeleteModels(ICollection<Stack> values) {
            await _eventRepository.RemoveAllByStackIdsAsync(values.Select(s => s.Id).ToArray());
            await base.DeleteModels(values);
        }

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        [HttpGet]
        [Route]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult Get(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, sort, time, offset, mode, page, limit);
        }

        private IHttpActionResult GetInternal(string systemFilter, string userFilter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
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
                Log.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Sort = sort, Time = time, Offset = offset, Page = page, Limit = limit })
                    .Tag("Search")
                    .Property("User", ExceptionlessUser)
                    .ContextProperty("HttpActionContext", ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.InvariantCultureIgnoreCase))
                return OkWithResourceLinks(GetStackSummaries(stacks, timeInfo.Offset, timeInfo.UtcRange.UtcStart, timeInfo.UtcRange.UtcEnd), options.HasMore, page);

            return OkWithResourceLinks(stacks, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        /// <summary>
        /// Get by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/stacks")]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult GetByOrganization(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            return GetInternal(String.Concat("organization:", organizationId), filter, sort, time, offset, mode, page, limit);
        }

        /// <summary>
        /// Get newest
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("new")]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult New(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, "-first", String.Concat("first|", time), offset, mode, page, limit);
        }

        /// <summary>
        /// Get newest by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/new")]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult NewByProject(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, "-first", String.Concat("first|", time), offset, mode, page, limit);
        }

        /// <summary>
        /// Get most recent
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        [HttpGet]
        [Route("recent")]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult Recent(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternal(null, filter, "-last", String.Concat("last|", time), offset, mode, page, limit);
        }

        /// <summary>
        /// Get most recent by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/recent")]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult RecentByProject(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, "-last", String.Concat("last|", time), offset, mode, page, limit);
        }

        /// <summary>
        /// Get most frequent
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        [HttpGet]
        [Route("frequent")]
        [ResponseType(typeof(List<Stack>))]
        public IHttpActionResult Frequent(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return FrequentInternal(null, filter, time, offset, mode, page, limit);
        }

        private IHttpActionResult FrequentInternal(string systemFilter = null, string userFilter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
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
                Log.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Time = time, Offset = offset, Page = page, Limit = limit })
                    .Tag("Search")
                    .Property("User", ExceptionlessUser)
                    .ContextProperty("HttpActionContext", ActionContext)
                    .Write();

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

        /// <summary>
        /// Gets most frequent by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole event object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/frequent")]
        [ResponseType(typeof(List<Stack>))]
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