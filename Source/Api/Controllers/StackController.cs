﻿using System;
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
using Exceptionless.Core.Filter;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models.Stats;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stacks")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StackController : RepositoryApiController<IStackRepository, Stack, Stack, Stack, Stack> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly IWebHookRepository _webHookRepository;
        private readonly WebHookDataPluginManager _webHookDataPluginManager;
        private readonly ICacheClient _cacheClient;
        private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
        private readonly EventStats _eventStats;
        private readonly BillingManager _billingManager;
        private readonly FormattingPluginManager _formattingPluginManager;
        private readonly List<FieldAggregation> _distinctUsersFields = new List<FieldAggregation> { new FieldAggregation { Field = "user.raw", Type = FieldAggregationType.Distinct } };
        private readonly List<FieldAggregation> _distinctUsersFieldsWithSort = new List<FieldAggregation> { new FieldAggregation { Field = "user.raw", Type = FieldAggregationType.Distinct, SortOrder = SortOrder.Descending } };

        public StackController(IStackRepository stackRepository,  IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IQueue<WorkItemData> workItemQueue, IWebHookRepository webHookRepository, WebHookDataPluginManager webHookDataPluginManager, IQueue<WebHookNotification> webHookNotificationQueue, ICacheClient cacheClient, EventStats eventStats, BillingManager billingManager, FormattingPluginManager formattingPluginManager, ILoggerFactory loggerFactory, IMapper mapper) : base(stackRepository, loggerFactory, mapper) {
            _stackRepository = stackRepository;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _workItemQueue = workItemQueue;
            _webHookRepository = webHookRepository;
            _webHookDataPluginManager = webHookDataPluginManager;
            _webHookNotificationQueue = webHookNotificationQueue;
            _cacheClient = cacheClient;
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
        /// <response code="404">One or more stacks could not be found.</response>
        [HttpPost]
        [Route("{ids:objectids}/mark-fixed")]
        public async Task<IHttpActionResult> MarkFixedAsync(string ids) {
            var stacks = await GetModelsAsync(ids.FromDelimitedString(), false);
            if (!stacks.Any())
                return NotFound();

            var stacksToUpdate = stacks.Where(s => !s.DateFixed.HasValue).ToList();
            if (stacksToUpdate.Count > 0) {
                foreach (var stack in stacksToUpdate) {
                    // TODO: Implement Fixed in version.
                    stack.DateFixed = DateTime.UtcNow;
                    //stack.FixedInVersion = "GET CURRENT VERSION FROM ELASTIC SEARCH";
                    stack.IsRegressed = false;
                }

                await _stackRepository.SaveAsync(stacksToUpdate);
            }

            var workIds = new List<string>();
            foreach (var stack in stacks)
                workIds.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
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
            var stack = await GetModelAsync(id, false);
            if (stack == null)
                return NotFound();

            if (String.IsNullOrEmpty(url))
                return BadRequest();

            if (!stack.References.Contains(url)) {
                stack.References.Add(url);
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
            var stack = await GetModelAsync(id, false);
            if (stack == null)
                return NotFound();

            if (String.IsNullOrEmpty(url))
                return BadRequest();

            if (stack.References.Contains(url)) {
                stack.References.Remove(url);
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
                foreach (var stack in stacksToUpdate) {
                    stack.DateFixed = null;
                    stack.IsRegressed = false;
                }

                await _stackRepository.SaveAsync(stacksToUpdate);
            }

            var workIds = new List<string>();
            foreach (var stack in stacks)
                workIds.Add(await _workItemQueue.EnqueueAsync(new StackWorkItem {
                    OrganizationId = stack.OrganizationId,
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
                    OrganizationId = hook.OrganizationId,
                    ProjectId = hook.ProjectId,
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
        [HttpGet]
        [Route]
        [ResponseType(typeof(List<Stack>))]
        public Task<IHttpActionResult> GetAsync(string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternalAsync(null, filter, sort, time, offset, mode, page, limit);
        }

        private async Task<IHttpActionResult> GetInternalAsync(string systemFilter, string userFilter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page + 1, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var pr = QueryProcessor.Process(userFilter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            var organizations = await GetAssociatedOrganizationsAsync(_organizationRepository);
            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            systemFilter = String.Join(" ", new[] { systemFilter, BuildSystemFilter(organizations, userFilter, pr.UsesPremiumFeatures, "last") }.Where(f => !String.IsNullOrWhiteSpace(f)));
            
            var sortBy = GetSort(sort);
            var options = new PagingOptions { Page = page, Limit = limit };
            
            try {
                var results = await _repository.GetByFilterAsync(systemFilter, userFilter, sortBy, ti.Field, ti.UtcRange.Start, ti.UtcRange.End, options);

                var stacks = results.Documents.Select(s => s.ApplyOffset(ti.Offset)).ToList();
                if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase))
                    return OkWithResourceLinks(await GetStackSummariesAsync(stacks, ti.Offset, systemFilter, ti.UtcRange.Start, ti.UtcRange.End), results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);

                return OkWithResourceLinks(stacks, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Sort = sort, Time = time, Offset = offset, Page = page, Limit = limit })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
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
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/stacks")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> GetByOrganizationAsync(string organizationId = null, string filter = null, string sort = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return NotFound();

            return await GetInternalAsync(String.Concat("organization:", organizationId), filter, sort, time, offset, mode, page, limit);
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
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("new")]
        [ResponseType(typeof(List<Stack>))]
        public Task<IHttpActionResult> NewAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternalAsync(null, filter, "-first", String.Concat("first|", time), offset, mode, page, limit);
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
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/new")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> NewByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetInternalAsync(String.Concat("project:", projectId), filter, "-first", String.Concat("first|", time), offset, mode, page, limit);
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
        [HttpGet]
        [Route("recent")]
        [ResponseType(typeof(List<Stack>))]
        public Task<IHttpActionResult> RecentAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetInternalAsync(null, filter, "-last", String.Concat("last|", time), offset, mode, page, limit);
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
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/recent")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> RecentByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetInternalAsync(String.Concat("project:", projectId), filter, "-last", String.Concat("last|", time), offset, mode, page, limit);
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
        [HttpGet]
        [Route("frequent")]
        [ResponseType(typeof(List<Stack>))]
        public Task<IHttpActionResult> FrequentAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetAllByTermsAsync(_distinctUsersFields, null, filter, time, offset, mode, page, limit);
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
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/frequent")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> FrequentByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetAllByTermsAsync(_distinctUsersFields, String.Concat("project:", projectId), filter, time, offset, mode, page, limit);
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
        [HttpGet]
        [Route("users")]
        [ResponseType(typeof(List<Stack>))]
        public Task<IHttpActionResult> UsersAsync(string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            return GetAllByTermsAsync(_distinctUsersFieldsWithSort, null, filter, time, offset, mode, page, limit);
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
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stacks/users")]
        [ResponseType(typeof(List<Stack>))]
        public async Task<IHttpActionResult> UsersByProjectAsync(string projectId, string filter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            return await GetAllByTermsAsync(_distinctUsersFieldsWithSort, String.Concat("project:", projectId), filter, time, offset, mode, page, limit);
        }

        private async Task<IHttpActionResult> GetAllByTermsAsync(ICollection<FieldAggregation> fields, string systemFilter = null, string userFilter = null, string time = null, string offset = null, string mode = null, int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var skip = GetSkip(page, limit);
            if (skip > MAXIMUM_SKIP)
                return Ok(new object[0]);

            var pr = QueryProcessor.Process(userFilter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            var organizations = await GetAssociatedOrganizationsAsync(_organizationRepository);
            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());
            systemFilter = String.Join(" ", new []{ systemFilter, BuildSystemFilter(organizations, userFilter, pr.UsesPremiumFeatures) }.Where(f => !String.IsNullOrWhiteSpace(f)));

            try {
                var ntsr = await _eventStats.GetNumbersTermsStatsAsync("stack_id", fields, ti.UtcRange.Start, ti.UtcRange.End, systemFilter, userFilter, ti.Offset, GetSkip(page + 1, limit) + 1);
                if (ntsr.Terms.Count == 0)
                    return Ok(new object[0]);

                var stackIds = ntsr.Terms.Skip(skip).Take(limit + 1).Select(t => t.Term).ToArray();
                var stacks = (await _stackRepository.GetByIdsAsync(stackIds)).Documents.Select(s => s.ApplyOffset(ti.Offset)).ToList();

                if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase)) {
                    var summaries = await GetStackSummariesAsync(stacks, ntsr, systemFilter, ti.UtcRange.Start, ti.UtcRange.End);
                    return OkWithResourceLinks(summaries.Take(limit).ToList(), summaries.Count > limit, page);
                }

                return OkWithResourceLinks(stacks.Take(limit).ToList(), stacks.Count > limit, page);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Time = time, Offset = offset, Page = page, Limit = limit })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }
        }

        private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, TimeSpan offset, string systemFilter, DateTime utcStart, DateTime utcEnd) {
            if (stacks.Count == 0)
                return new List<StackSummaryModel>();
            
            var ntsr = await _eventStats.GetNumbersTermsStatsAsync("stack_id", _distinctUsersFields, utcStart, utcEnd, systemFilter, String.Join(" OR ", stacks.Select(r => $"stack:{r.Id}")), offset, stacks.Count);
            return await GetStackSummariesAsync(stacks, ntsr, systemFilter, utcStart, utcEnd);
        }

        private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, NumbersTermStatsResult stackTerms, string systemFilter, DateTime utcStart, DateTime utcEnd) {
            if (stacks.Count == 0)
                return new List<StackSummaryModel>(0);

            var totalUsers = await GetUserCountByProjectIdsAsync(stacks, systemFilter, utcStart, utcEnd);
            return stacks.Join(stackTerms.Terms, s => s.Id, tk => tk.Term, (stack, term) => {
                var data = _formattingPluginManager.GetStackSummaryData(stack);
                var summary = new StackSummaryModel {
                    TemplateKey = data.TemplateKey,
                    Data = data.Data,
                    Id = stack.Id,
                    Title = stack.Title,
                    FirstOccurrence = term.FirstOccurrence,
                    LastOccurrence = term.LastOccurrence,
                    Total = term.Total,

                    Users = term.Numbers[0],
                    TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
                };

                return summary;
            }).ToList();
        }
        
        private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, string systemFilter, DateTime utcStart, DateTime utcEnd) {
            var scopedCacheClient = new ScopedCacheClient(_cacheClient, $"project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
            var projectIds = stacks.Select(s => s.ProjectId).Distinct().ToList();
            var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);

            var totals = cachedTotals.Where(kvp => kvp.Value.HasValue).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
            if (totals.Count == projectIds.Count)
                return totals;

            var projects = cachedTotals.Where(kvp => !kvp.Value.HasValue).Select(kvp => new Project { Id = kvp.Key, OrganizationId = stacks.FirstOrDefault(s => s.ProjectId == kvp.Key)?.OrganizationId }).ToList();
            var projectTerms = await _eventStats.GetNumbersTermsStatsAsync("project_id", _distinctUsersFields, utcStart, utcEnd, systemFilter, projects.BuildRetentionFilter());

            // Cache all projects that have more than 10 users for 5 minutes.
            await scopedCacheClient.SetAllAsync(projectTerms.Terms.Where(t => t.Numbers[0] >= 10).ToDictionary(t => t.Term, t => t.Numbers[0]), TimeSpan.FromMinutes(5));
            totals.AddRange(projectTerms.Terms.ToDictionary(kvp => kvp.Term, kvp => kvp.Numbers[0]));

            return totals;
        }
        
        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, useCache);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return null;

            return project;
        }
    }
}
