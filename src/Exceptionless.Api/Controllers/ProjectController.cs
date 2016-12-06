using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Processors;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Foundatio.Utility;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/projects")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : RepositoryApiController<IProjectRepository, Project, ViewProject, NewProject, UpdateProject> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly BillingManager _billingManager;

        public ProjectController(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IEventRepository eventRepository, IQueue<WorkItemData> workItemQueue, BillingManager billingManager, IMapper mapper, ILoggerFactory loggerFactory) : base(projectRepository, mapper, loggerFactory) {
            _organizationRepository = organizationRepository;
            _eventRepository = eventRepository;
            _workItemQueue = workItemQueue;
            _billingManager = billingManager;
        }

        #region CRUD

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <param name="mode">If no mode is set then the a light weight project object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
        [HttpGet]
        [Route]
        [ResponseType(typeof(List<ViewProject>))]
        public async Task<IHttpActionResult> GetAsync(int page = 1, int limit = 10, string mode = null) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var projects = await _repository.GetByOrganizationIdsAsync(GetAssociatedOrganizationIds(), options);
            var viewProjects = await MapCollectionAsync<ViewProject>(projects.Documents, true);

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
                return OkWithResourceLinks(await PopulateProjectStatsAsync(viewProjects.ToList()), projects.HasMore && !NextPageExceedsSkipLimit(page, limit), page, projects.Total);

            return OkWithResourceLinks(viewProjects, projects.HasMore && !NextPageExceedsSkipLimit(page, limit), page, projects.Total);
        }

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="organization">The identifier of the organization.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <param name="mode">If no mode is set then the a light weight project object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organization:objectid}/projects")]
        [ResponseType(typeof(List<ViewProject>))]
        public async Task<IHttpActionResult> GetByOrganizationAsync(string organization, int page = 1, int limit = 10, string mode = null) {
            if (String.IsNullOrEmpty(organization) || !CanAccessOrganization(organization))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var projects = await _repository.GetByOrganizationIdAsync(organization, options, true);
            var viewProjects = (await MapCollectionAsync<ViewProject>(projects.Documents, true)).ToList();

            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
                return OkWithResourceLinks(await PopulateProjectStatsAsync(viewProjects), projects.HasMore && !NextPageExceedsSkipLimit(page, limit), page, projects.Total);

            return OkWithResourceLinks(viewProjects, projects.HasMore && !NextPageExceedsSkipLimit(page, limit), page, projects.Total);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="mode">If no mode is set then the a light weight project object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}", Name = "GetProjectById")]
        [ResponseType(typeof(ViewProject))]
        public async Task<IHttpActionResult> GetByIdAsync(string id, string mode = null) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            var viewProject = await MapAsync<ViewProject>(project, true);
            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "stats", StringComparison.OrdinalIgnoreCase))
              return Ok(await PopulateProjectStatsAsync(viewProject));

            return Ok(viewProject);
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns></returns>
        /// <response code="400">An error occurred while creating the project.</response>
        /// <response code="409">The project already exists.</response>
        [HttpPost]
        [Route]
        [ResponseType(typeof(ViewProject))]
        public override Task<IHttpActionResult> PostAsync(NewProject project) {
            return base.PostAsync(project);
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="changes">The changes</param>
        /// <response code="400">An error occurred while updating the project.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpPatch]
        [HttpPut]
        [Route("{id:objectid}")]
        public override Task<IHttpActionResult> PatchAsync(string id, Delta<UpdateProject> changes) {
            return base.PatchAsync(id, changes);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of project identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more projects were not found.</response>
        /// <response code="500">An error occurred while deleting one or more projects.</response>
        [HttpDelete]
        [Route("{ids:objectids}")]
        public Task<IHttpActionResult> DeleteAsync(string ids) {
            return base.DeleteAsync(ids.FromDelimitedString());
        }

        #endregion

        /// <summary>
        /// Get configuration settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="v">The client configuration version.</param>
        /// <response code="304">The client configuration version is the current version.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("config")]
        [Route("{id:objectid}/config")]
        [Route("~/api/v1/project/config")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ResponseType(typeof(ClientConfiguration))]
        public async Task<IHttpActionResult> GetConfigAsync(string id = null, int? v = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetProjectId();

            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            if (v.HasValue && v == project.Configuration.Version)
                return StatusCode(HttpStatusCode.NotModified);

            return Ok(project.Configuration);
        }

        /// <summary>
        /// Add configuration value
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the configuration object.</param>
        /// <param name="value">The configuration value.</param>
        /// <response code="400">Invalid configuration value.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpPost]
        [Route("{id:objectid}/config")]
        public async Task<IHttpActionResult> SetConfigAsync(string id, string key, [NakedBody] string value) {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            project.Configuration.Settings[key.Trim()] = value.Trim();
            project.Configuration.IncrementVersion();
            await _repository.SaveAsync(project, true);

            return Ok();
        }

        /// <summary>
        /// Remove configuration value
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the configuration object.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete]
        [Route("{id:objectid}/config")]
        public async Task<IHttpActionResult> DeleteConfigAsync(string id, string key) {
            if (String.IsNullOrWhiteSpace(key))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.Configuration.Settings.Remove(key.Trim())) {
                project.Configuration.IncrementVersion();
                await _repository.SaveAsync(project, true);
            }

            return Ok();
        }

        /// <summary>
        /// Reset project data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}/reset-data")]
        public async Task<IHttpActionResult> ResetDataAsync(string id) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            string workItemId = await _workItemQueue.EnqueueAsync(new RemoveProjectWorkItem {
                ProjectId = project.Id,
                Reset = true
            });

            return WorkInProgress(new [] { workItemId });
        }

        [HttpGet]
        [Route("{id:objectid}/notifications")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> GetNotificationSettingsAsync(string id) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            return Ok(project.NotificationSettings);
        }

        /// <summary>
        /// Get user notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="userId">The identifier of the user.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        [ResponseType(typeof(NotificationSettings))]
        public async Task<IHttpActionResult> GetNotificationSettingsAsync(string id, string userId) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(CurrentUser.Id, userId))
                return NotFound();

            NotificationSettings settings;
            return Ok(project.NotificationSettings.TryGetValue(userId, out settings) ? settings : new NotificationSettings());
        }

        /// <summary>
        /// Set user notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="userId">The identifier of the user.</param>
        /// <param name="settings">The notification settings.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpPut]
        [HttpPost]
        [Route("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        public async Task<IHttpActionResult> SetNotificationSettingsAsync(string id, string userId, NotificationSettings settings) {
            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(CurrentUser.Id, userId))
                return NotFound();

            if (settings == null)
                project.NotificationSettings.Remove(userId);
            else
                project.NotificationSettings[userId] = settings;

            await _repository.SaveAsync(project, true);
            return Ok();
        }

        /// <summary>
        /// Remove user notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="userId">The identifier of the user.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete]
        [Route("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        public async Task<IHttpActionResult> DeleteNotificationSettingsAsync(string id, string userId) {
            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(CurrentUser.Id, userId))
                return NotFound();

            if (project.NotificationSettings.ContainsKey(userId)) {
                project.NotificationSettings.Remove(userId);
                await _repository.SaveAsync(project, true);
            }

            return Ok();
        }

        /// <summary>
        /// Promote tab
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="name">The tab name.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpPut]
        [HttpPost]
        [Route("{id:objectid}/promotedtabs")]
        public async Task<IHttpActionResult> PromoteTabAsync(string id, string name) {
            if (String.IsNullOrWhiteSpace(name))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (!project.PromotedTabs.Contains(name.Trim())) {
                project.PromotedTabs.Add(name.Trim());
                await _repository.SaveAsync(project, true);
            }

            return Ok();
        }

        /// <summary>
        /// Demote tab
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="name">The tab name.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete]
        [Route("{id:objectid}/promotedtabs")]
        public async Task<IHttpActionResult> DemoteTabAsync(string id, string name) {
            if (String.IsNullOrWhiteSpace(name))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.PromotedTabs.Contains(name.Trim())) {
                project.PromotedTabs.Remove(name.Trim());
                await _repository.SaveAsync(project, true);
            }

            return Ok();
        }

        /// <summary>
        /// Check for unique name
        /// </summary>
        /// <param name="name">The project name to check.</param>
        /// <param name="organizationId">If set the check name will be scoped to a specific organization.</param>
        /// <response code="201">The project name is available.</response>
        /// <response code="204">The project name is not available.</response>
        [HttpGet]
        [Route("check-name")]
        [Route("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/projects/check-name")]        
        public async Task<IHttpActionResult> IsNameAvailableAsync(string name, string organizationId = null) {
            if (await IsProjectNameAvailableInternalAsync(organizationId, name))
                return StatusCode(HttpStatusCode.NoContent);

            return StatusCode(HttpStatusCode.Created);
        }

        private async Task<bool> IsProjectNameAvailableInternalAsync(string organizationId, string name) {
            if (String.IsNullOrWhiteSpace(name))
                return false;

            var organizationIds = IsInOrganization(organizationId) ? new List<string> { organizationId } : GetAssociatedOrganizationIds();
            var projects = await _repository.GetByOrganizationIdsAsync(organizationIds);

            string decodedName = Uri.UnescapeDataString(name).Trim().ToLower();
            return !projects.Documents.Any(p => String.Equals(p.Name.Trim().ToLower(), decodedName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Add custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <param name="value">Any string value.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpPost]
        [Route("{id:objectid}/data")]
        public async Task<IHttpActionResult> PostDataAsync(string id, string key, [NakedBody]string value) {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            project.Data[key.Trim()] = value.Trim();
            await _repository.SaveAsync(project, true);

            return Ok();
        }

        /// <summary>
        /// Remove custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete]
        [Route("{id:objectid}/data")]
        public async Task<IHttpActionResult> DeleteDataAsync(string id, string key) {
            if (String.IsNullOrWhiteSpace(key))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.Data.Remove(key.Trim()))
                await _repository.SaveAsync(project, true);

            return Ok();
        }

        protected override async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models) {
            await base.AfterResultMapAsync(models);

            // TODO: We can optimize this by normalizing the project model to include the organization name.
            var viewProjects = models.OfType<ViewProject>().ToList();
            var organizations = await _organizationRepository.GetByIdsAsync(viewProjects.Select(p => p.OrganizationId).ToArray(), true);
            foreach (var viewProject in viewProjects) {
                var organization = organizations.FirstOrDefault(o => o.Id == viewProject.OrganizationId);
                if (organization != null) {
                    viewProject.OrganizationName = organization.Name;
                    viewProject.HasPremiumFeatures = organization.HasPremiumFeatures;
                }

                if (!viewProject.IsConfigured.HasValue) {
                    viewProject.IsConfigured = true;
                    await _workItemQueue.EnqueueAsync(new SetProjectIsConfiguredWorkItem {
                        ProjectId = viewProject.Id
                    });
                }
            }
        }

        protected override async Task<PermissionResult> CanAddAsync(Project value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithMessage("Project name is required.");

            if (!await IsProjectNameAvailableInternalAsync(value.OrganizationId, value.Name))
                return PermissionResult.DenyWithMessage("A project with this name already exists.");

            if (!await _billingManager.CanAddProjectAsync(value))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add additional projects.");

            return await base.CanAddAsync(value);
        }

        protected override Task<Project> AddModelAsync(Project value) {
            value.IsConfigured = false;
            value.NextSummaryEndOfDayTicks = SystemClock.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            value.AddDefaultOwnerNotificationSettings(CurrentUser.Id);
            value.SetDefaultUserAgentBotPatterns();
            value.Configuration.IncrementVersion();

            return base.AddModelAsync(value);
        }

        protected override async Task<PermissionResult> CanUpdateAsync(Project original, Delta<UpdateProject> changes) {
            var changed = changes.GetEntity();
            if (changes.ContainsChangedProperty(p => p.Name) && !await IsProjectNameAvailableInternalAsync(original.OrganizationId, changed.Name))
                return PermissionResult.DenyWithMessage("A project with this name already exists.");

            return await base.CanUpdateAsync(original, changes);
        }

        protected override async Task<IEnumerable<string>> DeleteModelsAsync(ICollection<Project> projects) {
            var workItems = new List<string>();
            foreach (var project in projects) {
                workItems.Add(await _workItemQueue.EnqueueAsync(new RemoveProjectWorkItem {
                    ProjectId = project.Id,
                    Reset = false
                }));
            }

            return workItems;
        }

        private async Task<ViewProject> PopulateProjectStatsAsync(ViewProject project) {
            return (await PopulateProjectStatsAsync(new List<ViewProject> { project })).FirstOrDefault();
        }

        private async Task<List<ViewProject>> PopulateProjectStatsAsync(List<ViewProject> viewProjects) {
            if (viewProjects.Count <= 0)
                return viewProjects;

            var organizations = await _organizationRepository.GetByIdsAsync(viewProjects.Select(p => p.OrganizationId).ToArray(), true);
            var projects = viewProjects.Select(p => new Project { Id = p.Id, OrganizationId = p.OrganizationId }).ToList();
            var sf = new ExceptionlessSystemFilterQuery(projects, organizations);
            var systemFilter = new ElasticQuery().WithSystemFilter(sf).WithDateRange(organizations.GetRetentionUtcCutoff(), DateTime.MaxValue, "date").WithIndexes(organizations.GetRetentionUtcCutoff(), DateTime.MaxValue);
            var result = await _eventRepository.CountBySearchAsync(systemFilter, null, $"terms:(project_id~{viewProjects.Count} cardinality:stack_id)");
            foreach (var project in viewProjects) {
                var term = result.Aggregations.Terms<string>("terms_project_id").Buckets.FirstOrDefault(t => t.Key == project.Id);
                project.EventCount = term?.DocCount ?? 0;
                project.StackCount = (long)(term?.Aggregations.Cardinality("cardinality_stack_id").Value ?? 0);
            }

            return viewProjects;
        }
    }
}
