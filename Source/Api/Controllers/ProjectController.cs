using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
using Exceptionless.Core.Models;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/projects")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : RepositoryApiController<IProjectRepository, Project, ViewProject, NewProject, UpdateProject> {
        private readonly OrganizationRepository _organizationRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IWebHookRepository _webHookRepository;
        private readonly BillingManager _billingManager; 
        private readonly DataHelper _dataHelper;
        private readonly EventStats _stats;

        public ProjectController(IProjectRepository projectRepository, OrganizationRepository organizationRepository, ITokenRepository tokenRepository, IWebHookRepository webHookRepository, BillingManager billingManager, DataHelper dataHelper, EventStats stats) : base(projectRepository) {
            _organizationRepository = organizationRepository;
            _tokenRepository = tokenRepository;
            _webHookRepository = webHookRepository;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
            _stats = stats;
        }

        #region CRUD

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        [HttpGet]
        [Route]
        [ResponseType(typeof(List<ViewProject>))]
        public async Task<IHttpActionResult> GetAsync(int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var projects = await _repository.GetByOrganizationIdsAsync(GetAssociatedOrganizationIds(), options).AnyContext();
            var viewProjects = (await MapCollectionAsync<ViewProject>(projects.Documents, true).AnyContext()).ToList();
            return OkWithResourceLinks(await PopulateProjectStatsAsync(viewProjects).AnyContext(), options.HasMore, page, projects.Total);
        }

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="organization">The identifier of the organization.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The organization could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organization:objectid}/projects")]
        [ResponseType(typeof(List<ViewProject>))]
        public async Task<IHttpActionResult> GetByOrganizationAsync(string organization, int page = 1, int limit = 10) {
            if (!String.IsNullOrEmpty(organization) && !CanAccessOrganization(organization))
                return NotFound();

            var organizationIds = new List<string>();
            if (!String.IsNullOrEmpty(organization) && CanAccessOrganization(organization))
                organizationIds.Add(organization);
            else
                organizationIds.AddRange(GetAssociatedOrganizationIds());

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var projects = await _repository.GetByOrganizationIdsAsync(organizationIds, options).AnyContext();
            var viewProjects = (await MapCollectionAsync<ViewProject>(projects.Documents, true).AnyContext()).ToList();
            return OkWithResourceLinks(await PopulateProjectStatsAsync(viewProjects).AnyContext(), options.HasMore && !NextPageExceedsSkipLimit(page, limit), page, projects.Total);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}", Name = "GetProjectById")]
        [ResponseType(typeof(ViewProject))]
        public override async Task<IHttpActionResult> GetByIdAsync(string id) {
            var project = await GetModelAsync(id).AnyContext();
            if (project == null)
                return NotFound();

            var viewProject = await MapAsync<ViewProject>(project, true).AnyContext();
            return Ok(await PopulateProjectStatsAsync(viewProject).AnyContext());
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
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("config")]
        [Route("{id:objectid}/config")]
        [Route("~/api/v1/project/config")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ResponseType(typeof(ClientConfiguration))]
        public async Task<IHttpActionResult> GetConfigAsync(string id = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetProjectId();

            var project = await GetModelAsync(id).AnyContext();
            if (project == null)
                return NotFound();

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
        [Route("{id:objectid}/config/{key:minlength(1)}")]
        public async Task<IHttpActionResult> SetConfigAsync(string id, string key, [NakedBody] string value) {
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (String.IsNullOrWhiteSpace(value))
                return BadRequest();

            project.Configuration.Settings[key] = value;
            project.Configuration.IncrementVersion();
            await _repository.SaveAsync(project, true).AnyContext();

            return Ok();
        }

        /// <summary>
        /// Remove configuration value
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the configuration object.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete]
        [Route("{id:objectid}/config/{key:minlength(1)}")]
        public async Task<IHttpActionResult> DeleteConfigAsync(string id, string key) {
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (project.Configuration.Settings.Remove(key)) {
                project.Configuration.IncrementVersion(); 
                await _repository.SaveAsync(project, true).AnyContext();
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
            var project = await GetModelAsync(id).AnyContext();
            if (project == null)
                return NotFound();

            // TODO: Implement a long running process queue where a task can be inserted and then monitor for progress.
            await _dataHelper.ResetProjectDataAsync(id).AnyContext();

            return Ok();
        }

        [HttpGet]
        [Route("{id:objectid}/notifications")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> GetNotificationSettingsAsync(string id) {
            var project = await GetModelAsync(id).AnyContext();
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
            var project = await GetModelAsync(id).AnyContext();
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
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
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
                return NotFound();

            if (settings == null)
                project.NotificationSettings.Remove(userId);
            else
                project.NotificationSettings[userId] = settings;

            await _repository.SaveAsync(project, true).AnyContext();
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
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
                return NotFound();

            if (project.NotificationSettings.ContainsKey(userId)) {
                project.NotificationSettings.Remove(userId);
                await _repository.SaveAsync(project, true).AnyContext();
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
        [Route("{id:objectid}/promotedtabs/{name:minlength(1)}")]
        public async Task<IHttpActionResult> PromoteTabAsync(string id, string name) {
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (!project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Add(name);
                await _repository.SaveAsync(project, true).AnyContext();
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
        [Route("{id:objectid}/promotedtabs/{name:minlength(1)}")]
        public async Task<IHttpActionResult> DemoteTabAsync(string id, string name) {
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Remove(name);
                await _repository.SaveAsync(project, true).AnyContext();
            }

            return Ok();
        }

        /// <summary>
        /// Check for unique name
        /// </summary>
        /// <param name="name">The project name to check.</param>
        /// <response code="201">The project name is available.</response>
        /// <response code="204">The project name is not available.</response>
        [HttpGet]
        [Route("check-name/{*name:minlength(1)}")]
        public async Task<IHttpActionResult> IsNameAvailableAsync(string name) {
            if (await IsProjectNameAvailableInternalAsync(null, name).AnyContext())
                return StatusCode(HttpStatusCode.NoContent);

            return StatusCode(HttpStatusCode.Created);
        }

        private async Task<bool> IsProjectNameAvailableInternalAsync(string organizationId, string name) {
            if (String.IsNullOrWhiteSpace(name))
                return false;

            ICollection<string> organizationIds = !String.IsNullOrEmpty(organizationId) ? new List<string> { organizationId } : GetAssociatedOrganizationIds();
            return !(await _repository.GetByOrganizationIdsAsync(organizationIds).AnyContext()).Documents.Any(o => String.Equals(o.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Add custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <param name="value">Any string value.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpPost]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public async Task<IHttpActionResult> PostDataAsync(string id, string key, string value) {
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            project.Data[key] = value;
            await _repository.SaveAsync(project, true).AnyContext();

            return Ok();
        }

        /// <summary>
        /// Remove custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public async Task<IHttpActionResult> DeleteDataAsync(string id, string key) {
            var project = await GetModelAsync(id, false).AnyContext();
            if (project == null)
                return NotFound();

            if (project.Data.Remove(key))
                await _repository.SaveAsync(project, true).AnyContext();

            return Ok();
        }
        
        protected override async Task CreateMapsAsync() {
            if (Mapper.FindTypeMapFor<Project, ViewProject>() == null) {
                Mapper.CreateMap<Project, ViewProject>().AfterMap(async (p, pi) => {
                    try {
                        pi.OrganizationName = (await _organizationRepository.GetByIdAsync(p.OrganizationId, true).AnyContext()).Name;
                    } catch (Exception ex) {
                        Log.Error().Exception(ex).Message("Unable to load organization. Message: {0}", ex.Message).Write();
                    }
                });
            }

            await base.CreateMapsAsync().AnyContext();
        }

        protected override async Task<PermissionResult> CanAddAsync(Project value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithMessage("Project name is required.");

            if (!await IsProjectNameAvailableInternalAsync(value.OrganizationId, value.Name).AnyContext())
                return PermissionResult.DenyWithMessage("A project with this name already exists.");

            if (!await _billingManager.CanAddProjectAsync(value).AnyContext())
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add additional projects.");

            return await base.CanAddAsync(value).AnyContext();
        }

        protected override Task<Project> AddModelAsync(Project value) {
            value.NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            value.AddDefaultOwnerNotificationSettings(ExceptionlessUser.Id);
            return base.AddModelAsync(value);
        }

        protected override async Task<PermissionResult> CanUpdateAsync(Project original, Delta<UpdateProject> changes) {
            var changed = changes.GetEntity();
            if (changes.ContainsChangedProperty(p => p.Name) && !await IsProjectNameAvailableInternalAsync(original.OrganizationId, changed.Name).AnyContext())
                return PermissionResult.DenyWithMessage("A project with this name already exists.");

            return await base.CanUpdateAsync(original, changes).AnyContext();
        }

        protected override async Task DeleteModelsAsync(ICollection<Project> projects) {
            foreach (var project in projects) {
                await _tokenRepository.RemoveAllByProjectIdsAsync(new[] { project.Id }).AnyContext();
                await _webHookRepository.RemoveAllByProjectIdsAsync(new[] { project.Id }).AnyContext();
                await _dataHelper.ResetProjectDataAsync(project.Id).AnyContext();
            }

            await base.DeleteModelsAsync(projects).AnyContext();
        }

        private async Task<ViewProject> PopulateProjectStatsAsync(ViewProject project) {
            return (await PopulateProjectStatsAsync(new List<ViewProject> { project }).AnyContext()).FirstOrDefault();
        }

        private async Task<List<ViewProject>> PopulateProjectStatsAsync(List<ViewProject> projects) {
            if (projects.Count <= 0)
                return projects;

            var organizations = await _organizationRepository.GetByIdsAsync(projects.Select(p => p.Id).ToArray(), useCache: true).AnyContext();
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < projects.Count; index++) {
                if (index > 0)
                    builder.Append(" OR ");

                var project = projects[index];
                var organization = organizations.Documents.FirstOrDefault(o => o.Id == project.Id);
                if (organization != null && organization.RetentionDays > 0)
                    builder.AppendFormat("(project:{0} AND (date:[now/d-{1}d TO now/d+1d}} OR last:[now/d-{1}d TO now/d+1d}}))", project.Id, organization.RetentionDays);
                else
                    builder.AppendFormat("project:{0}", project.Id);
            }

            var result = _stats.GetTermsStats(DateTime.MinValue, DateTime.MaxValue, "project_id", builder.ToString());
            foreach (var project in projects) {
                var projectStats = result.Terms.FirstOrDefault(t => t.Term == project.Id);
                project.EventCount = projectStats?.Total ?? 0;
                project.StackCount = projectStats?.Unique ?? 0;
            }

            return projects;
        }
    }
}
