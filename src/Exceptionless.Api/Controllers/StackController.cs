using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Processors;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using McSherry.SemanticVersioning;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stacks")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StackController : RepositoryApiController<IStackRepository, Stack, Stack, Stack, Stack> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly IWebHookRepository _webHookRepository;
        private readonly WebHookDataPluginManager _webHookDataPluginManager;
        private readonly ICacheClient _cache;
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly BillingManager _billingManager;
        private readonly FormattingPluginManager _formattingPluginManager;

        public StackController(IStackRepository stackRepository,  IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IEventRepository eventRepository, IQueue<WorkItemData> workItemQueue, IWebHookRepository webHookRepository, WebHookDataPluginManager webHookDataPluginManager, IQueue<WebHookNotification> webHookNotificationQueue, ICacheClient cacheClient, BillingManager billingManager, FormattingPluginManager formattingPluginManager, IMapper mapper, ILoggerFactory loggerFactory) : base(stackRepository, mapper, loggerFactory) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
            _workItemQueue = workItemQueue;
            _webHookRepository = webHookRepository;
            _webHookDataPluginManager = webHookDataPluginManager;
            _webHookNotificationQueue = webHookNotificationQueue;
            _cache = cacheClient;
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
        public async Task<IHttpActionResult> GetByIdAsync(string id, string offset = null) {
            var stack = await GetModelAsync(id);
            if (stack == null)
                return NotFound();

            return Ok(stack.ApplyOffset(GetOffset(offset)));
        }

        /// <summary>
        /// Mark fixed
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <param name="version">A version number that the stack was fixed in.</param>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/mark-fixed")]
        public async Task<IHttpActionResult> MarkFixedAsync(string ids, string version = null) {
            version = version?.Trim();
            SemanticVersion semanticVersion = null;
            if (!String.IsNullOrEmpty(version) && !SemanticVersion.TryParse(version, out semanticVersion))
                return BadRequest("Invalid semantic version");

            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            var stacksToUpdate = stacks.Where(s => s.IsRegressed || !s.DateFixed.HasValue).ToList();
            if (stacksToUpdate.Count > 0) {
                foreach (var stack in stacksToUpdate)
                    stack.MarkFixed(semanticVersion);

                await _stackRepository.SaveAsync(stacksToUpdate);
            }

            var workIds = new List<string>();
            foreach (var stack in stacks)
                workIds.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
                    ProjectId = stack.ProjectId,
                    StackId = stack.Id,
                    UpdateIsFixed = true,
                    IsFixed = true
                }));

            return WorkInProgress(workIds);
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
        public async Task<IHttpActionResult> MarkFixedAsync(JObject data) {
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

            return await MarkFixedAsync(id);
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
        public async Task<IHttpActionResult> AddLinkAsync(string id, [NakedBody] string url) {
            if (String.IsNullOrWhiteSpace(url))
                return BadRequest();

            var stack = await GetModelAsync(id, false);
            if (stack == null)
                return NotFound();

            if (!stack.References.Contains(url.Trim())) {
                stack.References.Add(url.Trim());
                await _stackRepository.SaveAsync(stack);
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
        public async Task<IHttpActionResult> AddLinkAsync(JObject data) {
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
            return await AddLinkAsync(id, url);
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
        public async Task<IHttpActionResult> RemoveLinkAsync(string id, [NakedBody] string url) {
            if (String.IsNullOrWhiteSpace(url))
                return BadRequest();

            var stack = await GetModelAsync(id, false);
            if (stack == null)
                return NotFound();

            if (stack.References.Contains(url.Trim())) {
                stack.References.Remove(url.Trim());
                await _stackRepository.SaveAsync(stack);
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
        public async Task<IHttpActionResult> MarkCriticalAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => !s.OccurrencesAreCritical).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.OccurrencesAreCritical = true;

                await _stackRepository.SaveAsync(stacks);
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
        public async Task<IHttpActionResult> MarkNotCriticalAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => s.OccurrencesAreCritical).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.OccurrencesAreCritical = false;

                await _stackRepository.SaveAsync(stacks);
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
        public async Task<IHttpActionResult> EnableNotificationsAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => s.DisableNotifications).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.DisableNotifications = false;

                await _stackRepository.SaveAsync(stacks);
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
        public async Task<IHttpActionResult> DisableNotificationsAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            stacks = stacks.Where(s => !s.DisableNotifications).ToList();
            if (stacks.Count > 0) {
                foreach (var stack in stacks)
                    stack.DisableNotifications = true;

                await _stackRepository.SaveAsync(stacks);
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
        public async Task<IHttpActionResult> MarkNotFixedAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            var stacksToUpdate = stacks.Where(s => s.DateFixed.HasValue).ToList();
            if (stacksToUpdate.Count > 0) {
                foreach (var stack in stacksToUpdate)
                    stack.MarkNotFixed();

                await _stackRepository.SaveAsync(stacksToUpdate);
            }

            var workIds = new List<string>();
            foreach (var stack in stacks)
                workIds.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
                    ProjectId = stack.ProjectId,
                    StackId = stack.Id,
                    UpdateIsFixed = true,
                    IsFixed = false
                }));

            return WorkInProgress(workIds);
        }

        /// <summary>
        /// Mark hidden
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/mark-hidden")]
        public async Task<IHttpActionResult> MarkHiddenAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            var stacksToUpdate = stacks.Where(s => !s.IsHidden).ToList();
            if (stacksToUpdate.Count > 0) {
                foreach (var stack in stacksToUpdate)
                    stack.IsHidden = true;

                await _stackRepository.SaveAsync(stacksToUpdate);
            }

            var workIds = new List<string>();
            foreach (var stack in stacks)
                workIds.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
                    ProjectId = stack.ProjectId,
                    StackId = stack.Id,
                    UpdateIsHidden = true,
                    IsHidden = true
                }));

            return WorkInProgress(workIds);
        }

        /// <summary>
        /// Mark not hidden
        /// </summary>
        /// <param name="ids">A comma delimited list of stack identifiers.</param>
        /// <response code="204">The stacks were marked as not hidden.</response>
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpDelete]
        [Route("{ids:objectids}/mark-hidden")]
        public async Task<IHttpActionResult> MarkNotHiddenAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            var stacksToUpdate = stacks.Where(s => s.IsHidden).ToList();
            if (stacksToUpdate.Count > 0) {
                foreach (var stack in stacksToUpdate)
                    stack.IsHidden = false;

                await _stackRepository.SaveAsync(stacksToUpdate);
            }

            var workIds = new List<string>();
            foreach (var stack in stacks)
                workIds.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
                    ProjectId = stack.ProjectId,
                    StackId = stack.Id,
                    UpdateIsHidden = true,
                    IsHidden = false
                }));

            return WorkInProgress(workIds);
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
        public async Task<IHttpActionResult> PromoteAsync(string id) {
            if (String.IsNullOrEmpty(id))
                return NotFound();

            Stack stack = await _stackRepository.GetByIdAsync(id);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            if (!await _billingManager.HasPremiumFeaturesAsync(stack.OrganizationId))
                return PlanLimitReached("Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature.");

            List<WebHook> promotedProjectHooks = (await _webHookRepository.GetByProjectIdAsync(stack.ProjectId)).Documents.Where(p => p.EventTypes.Contains(WebHookRepository.EventTypes.StackPromoted)).ToList();
            if (!promotedProjectHooks.Any())
                return NotImplemented("No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.");

            foreach (WebHook hook in promotedProjectHooks) {
                var context = new WebHookDataContext(hook.Version, stack, isNew: stack.TotalOccurrences == 1, isRegression: stack.IsRegressed);
                await _webHookNotificationQueue.EnqueueAsync(new WebHookNotification {
                    OrganizationId = stack.OrganizationId,
                    ProjectId = stack.ProjectId,
                    WebHookId = hook.Id,
                    Url = hook.Url,
                    Data = await _webHookDataPluginManager.CreateFromStackAsync(context)
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
        public Task<IHttpActionResult> DeleteAsync(string ids) {
            return base.DeleteAsync(ids.FromDelimitedString());
        }

        protected override async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<Stack> stacks) {
            var workItems = new List<string>();
            foreach (var stack in stacks) {
                workItems.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
                    ProjectId = stack.ProjectId,
                    StackId = stack.Id,
                    Delete = true
                }));
            }

            return workItems;
        }

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Route]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> GetAsync(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var organizations = await GetAssociatedActiveOrganizationsAsync(_organizationRepository);
            if (organizations.Count == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
        }

        private async Task<IHttpActionResult> GetInternalAsync(IExceptionlessSystemFilterQuery sf, TimeInfo ti, string filter = null, string sort = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            int skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(EmptyModels);

            var pr = await QueryProcessor.ProcessAsync(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            sf.UsesPremiumFeatures = pr.UsesPremiumFeatures;
            var options = new PagingOptions { Page = page, Limit = limit };

            try {
                var results = await _repository.GetByFilterAsync(ShouldApplySystemFilter(sf, filter) ? sf : null, filter, sort, ti.Field, ti.UtcRange.Start, ti.UtcRange.End, options);

                var stacks = results.Documents.Select(s => s.ApplyOffset(ti.Offset)).ToList();
                if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase))
                    return OkWithResourceLinks(await GetStackSummariesAsync(stacks, sf, ti), results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);

                return OkWithResourceLinks(stacks, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex)
                    .Message("An error has occurred. Please check your search filter.")
                    .Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Sort = sort, Time = ti, Page = page, Limit = limit })
                    .Tag("Search")
                    .Identity(CurrentUser.EmailAddress)
                    .Property("User", CurrentUser)
                    .SetActionContext(ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }
        }

        /// <summary>
        /// Get by organization
        /// </summary>
        /// <param name="organizationId">The identifier of the organization.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="sort">Controls the sort order that the data is returned in. In this example -date returns the results descending by date.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The organization could not be found.</response>
        /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/stacks")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> GetByOrganizationAsync(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var organization = await GetOrganizationAsync(organizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(organization);
            return await GetInternalAsync(sf, ti, filter, sort, mode, page, limit);
        }

        /// <summary>
        /// Get newest
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Route("new")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> NewAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var organizations = await GetAssociatedActiveOrganizationsAsync(_organizationRepository);
            if (organizations.Count == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(String.Concat("first|", time), offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, filter, "-first", mode, page, limit);
        }

        /// <summary>
        /// Get newest by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/new")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> NewByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

            var ti = GetTimeInfo(String.Concat("first|", time), offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(project, organization);
            return await GetInternalAsync(sf, ti, filter, "-first", mode, page, limit);
        }

        /// <summary>
        /// Get most recent
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Route("recent")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> RecentAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var organizations = await GetAssociatedActiveOrganizationsAsync(_organizationRepository);
            if (organizations.Count == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(String.Concat("last|", time), offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(organizations) { IsUserOrganizationsFilter = true };
            return await GetInternalAsync(sf, ti, filter, "-last", mode, page, limit);
        }

        /// <summary>
        /// Get most recent by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/recent")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> RecentByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

            var ti = GetTimeInfo(String.Concat("last|", time), offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(project, organization);
            return await GetInternalAsync(sf, ti, filter, "-last", mode, page, limit);
        }

        /// <summary>
        /// Get most frequent
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Route("frequent")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> FrequentAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var organizations = await GetAssociatedActiveOrganizationsAsync(_organizationRepository);
            if (organizations.Count == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(organizations) { IsUserOrganizationsFilter = true };
            return await GetAllByTermsAsync("cardinality:user min:date max:date", sf, ti, filter, mode, page, limit);
        }

        /// <summary>
        /// Gets most frequent by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/frequent")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> FrequentByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(project, organization);
            return await GetAllByTermsAsync("cardinality:user min:date max:date", sf, ti, filter, mode, page, limit);
        }

        /// <summary>
        /// Get most users
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        [HttpGet]
        [Route("users")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> UsersAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var organizations = await GetAssociatedActiveOrganizationsAsync(_organizationRepository);
            if (organizations.Count == 0)
                return Ok(EmptyModels);

            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(organizations) { IsUserOrganizationsFilter = true };
            return await GetAllByTermsAsync("-cardinality:user min:date max:date", sf, ti, filter, mode, page, limit);
        }

        /// <summary>
        /// Gets most users by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <param name="mode">If no mode is set then the whole stack object will be returned. If the mode is set to summary than a light weight object will be returned.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="400">Invalid filter.</response>
        /// <response code="404">The project could not be found.</response>
        /// <response code="426">Unable to view stack occurrences for the suspended organization.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/users")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> UsersByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            var organization = await GetOrganizationAsync(project.OrganizationId);
            if (organization == null)
                return NotFound();

            if (organization.IsSuspended)
                return PlanLimitReached("Unable to view stack occurrences for the suspended organization.");

            var ti = GetTimeInfo(time, offset, organization.GetRetentionUtcCutoff());
            var sf = new ExceptionlessSystemFilterQuery(project, organization);
            return await GetAllByTermsAsync("-cardinality:user min:date max:date", sf, ti, filter, mode, page, limit);
        }

        private async Task<IHttpActionResult> GetAllByTermsAsync(string aggregations, IExceptionlessSystemFilterQuery sf, TimeInfo ti, string filter = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(EmptyModels);

            var pr = await QueryProcessor.ProcessAsync(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            sf.UsesPremiumFeatures = pr.UsesPremiumFeatures;

            try {
                var systemFilter = new ElasticQuery().WithSystemFilter(ShouldApplySystemFilter(sf, filter) ? sf : null).WithDateRange(ti.UtcRange.Start, ti.UtcRange.End, "date").WithIndexes(ti.UtcRange.Start, ti.UtcRange.End);
                var stackTerms = await _eventRepository.CountBySearchAsync(systemFilter, pr.ExpandedQuery, $"terms:(stack_id~{GetSkip(page + 1, limit) + 1} {aggregations})");
                if (stackTerms.Aggregations.Terms<string>("terms_stack_id").Buckets.Count == 0)
                    return Ok(EmptyModels);

                var stackIds = stackTerms.Aggregations.Terms<string>("terms_stack_id").Buckets.Skip(skip).Take(limit + 1).Select(t => t.Key).ToArray();
                var stacks = (await _stackRepository.GetByIdsAsync(stackIds)).Select(s => s.ApplyOffset(ti.Offset)).ToList();

                if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase)) {
                    var summaries = await GetStackSummariesAsync(stacks, stackTerms.Aggregations.Terms<string>("terms_stack_id").Buckets, sf, ti);
                    return OkWithResourceLinks(summaries.Take(limit).ToList(), summaries.Count > limit, page);
                }

                return OkWithResourceLinks(stacks.Take(limit).ToList(), stacks.Count > limit, page);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex)
                    .Message("An error has occurred. Please check your search filter.")
                    .Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Page = page, Limit = limit })
                    .Tag("Search")
                    .Identity(CurrentUser.EmailAddress)
                    .Property("User", CurrentUser)
                    .SetActionContext(ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }
        }

        private Task<Organization> GetOrganizationAsync(string organizationId, bool useCache = true) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return null;

            return _organizationRepository.GetByIdAsync(organizationId, useCache);
        }

        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, useCache);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return null;

            return project;
        }

        private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, IExceptionlessSystemFilterQuery eventSystemFilter, TimeInfo ti) {
            if (stacks.Count == 0)
                return new List<StackSummaryModel>();

            var systemFilter = new ElasticQuery().WithSystemFilter(eventSystemFilter).WithDateRange(ti.UtcRange.Start, ti.UtcRange.End, "date").WithIndexes(ti.UtcRange.Start, ti.UtcRange.End);
            var stackTerms = await _eventRepository.CountBySearchAsync(systemFilter, String.Join(" OR ", stacks.Select(r => $"stack:{r.Id}")), $"terms:(stack_id~{stacks.Count} cardinality:user min:date max:date)");
            return await GetStackSummariesAsync(stacks, stackTerms.Aggregations.Terms<string>("terms_stack_id").Buckets, eventSystemFilter, ti);
        }

        private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, IReadOnlyCollection<KeyedBucket<string>> stackTerms, IExceptionlessSystemFilterQuery sf, TimeInfo ti) {
            if (stacks.Count == 0)
                return new List<StackSummaryModel>(0);

            var totalUsers = await GetUserCountByProjectIdsAsync(stacks, sf, ti.UtcRange.Start, ti.UtcRange.End);
            return stacks.Join(stackTerms, s => s.Id, tk => tk.Key, (stack, term) => {
                var data = _formattingPluginManager.GetStackSummaryData(stack);
                var summary = new StackSummaryModel {
                    TemplateKey = data.TemplateKey,
                    Data = data.Data,
                    Id = stack.Id,
                    Title = stack.Title,
                    FirstOccurrence = term.Aggregations.Min<DateTime>("min_date").Value,
                    LastOccurrence = term.Aggregations.Max<DateTime>("max_date").Value,
                    Total = term.Total.GetValueOrDefault(),

                    Users = term.Aggregations.Cardinality("cardinality_user").Value.GetValueOrDefault(),
                    TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
                };

                return summary;
            }).ToList();
        }

        private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, IExceptionlessSystemFilterQuery sf, DateTime utcStart, DateTime utcEnd) {
            var scopedCacheClient = new ScopedCacheClient(_cache, $"Project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
            var projectIds = stacks.Select(s => s.ProjectId).Distinct().ToList();
            var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);

            var totals = cachedTotals.Where(kvp => kvp.Value.HasValue).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
            if (totals.Count == projectIds.Count)
                return totals;

            var systemFilter = new ElasticQuery().WithSystemFilter(sf).WithDateRange(utcStart, utcEnd, "date").WithIndexes(utcStart, utcEnd);
            var projects = cachedTotals.Where(kvp => !kvp.Value.HasValue).Select(kvp => new Project { Id = kvp.Key, OrganizationId = stacks.FirstOrDefault(s => s.ProjectId == kvp.Key)?.OrganizationId }).ToList();
            var result = await _eventRepository.CountBySearchAsync(systemFilter, projects.BuildFilter(), "terms:(project_id cardinality:user)");

            // Cache all projects that have more than 10 users for 5 minutes.
            var projectTerms = result.Aggregations.Terms<string>("terms_project_id").Buckets;
            await scopedCacheClient.SetAllAsync(projectTerms.Where(t => t.Total.GetValueOrDefault() >= 10).ToDictionary(t => t.Key, t => t.Total.GetValueOrDefault()), TimeSpan.FromMinutes(5));
            totals.AddRange(projectTerms.ToDictionary(t => t.Key, t => (double)t.Total.GetValueOrDefault()));

            return totals;
        }
    }
}
