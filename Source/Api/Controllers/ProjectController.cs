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
        public IHttpActionResult Get(int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var projects = _repository.GetByOrganizationIds(GetAssociatedOrganizationIds(), options).Select(Mapper.Map<Project, ViewProject>).ToList();
            return OkWithResourceLinks(PopulateProjectStats(projects), options.HasMore, page);
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
        public IHttpActionResult GetByOrganization(string organization, int page = 1, int limit = 10) {
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
            var projects = _repository.GetByOrganizationIds(organizationIds, options).Select(Mapper.Map<Project, ViewProject>).ToList();
            return OkWithResourceLinks(PopulateProjectStats(projects), options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}", Name = "GetProjectById")]
        [ResponseType(typeof(ViewProject))]
        public override IHttpActionResult GetById(string id) {
            var project = GetModel(id);
            if (project == null)
                return NotFound();

            var viewProject = Mapper.Map<Project, ViewProject>(project);
            return Ok(PopulateProjectStats(viewProject));
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
        public override IHttpActionResult Post(NewProject project) {
            return base.Post(project);
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
        public override IHttpActionResult Patch(string id, Delta<UpdateProject> changes) {
            return base.Patch(id, changes);
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
        public async Task<IHttpActionResult> DeleteAsync(string ids) {
            return await base.DeleteAsync(ids.FromDelimitedString());
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
        public IHttpActionResult GetConfig(string id = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetProjectId();

            var project = GetModel(id);
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
        public IHttpActionResult SetConfig(string id, string key, [NakedBody] string value) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (String.IsNullOrWhiteSpace(value))
                return BadRequest();

            project.Configuration.Settings[key] = value;
            project.Configuration.IncrementVersion();
            _repository.Save(project, true);

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
        public IHttpActionResult DeleteConfig(string id, string key) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.Configuration.Settings.Remove(key)) {
                project.Configuration.IncrementVersion(); 
                _repository.Save(project, true);
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
            var project = GetModel(id);
            if (project == null)
                return NotFound();

            // TODO: Implement a long running process queue where a task can be inserted and then monitor for progress.
            await _dataHelper.ResetProjectDataAsync(id);

            return Ok();
        }

        [HttpGet]
        [Route("{id:objectid}/notifications")]
        [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IHttpActionResult GetNotificationSettings(string id) {
            var project = GetModel(id);
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
        public IHttpActionResult GetNotificationSettings(string id, string userId) {
            var project = GetModel(id);
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
        public IHttpActionResult SetNotificationSettings(string id, string userId, NotificationSettings settings) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
                return NotFound();

            if (settings == null)
                project.NotificationSettings.Remove(userId);
            else
                project.NotificationSettings[userId] = settings;

            _repository.Save(project, true);

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
        public IHttpActionResult DeleteNotificationSettings(string id, string userId) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
                return NotFound();

            if (project.NotificationSettings.ContainsKey(userId)) {
                project.NotificationSettings.Remove(userId);
                _repository.Save(project, true);
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
        public IHttpActionResult PromoteTab(string id, string name) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (!project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Add(name);
                _repository.Save(project, true);
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
        public IHttpActionResult DemoteTab(string id, string name) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Remove(name);
                _repository.Save(project, true);
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
        public IHttpActionResult IsNameAvailable(string name) {
            if (IsProjectNameAvailableInternal(null, name))
                return StatusCode(HttpStatusCode.NoContent);

            return StatusCode(HttpStatusCode.Created);
        }

        private bool IsProjectNameAvailableInternal(string organizationId, string name) {
            if (String.IsNullOrWhiteSpace(name))
                return false;

            ICollection<string> organizationIds = !String.IsNullOrEmpty(organizationId) ? new List<string> { organizationId } : GetAssociatedOrganizationIds();
            return !_repository.GetByOrganizationIds(organizationIds).Any(o => String.Equals(o.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
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
        public IHttpActionResult PostData(string id, string key, string value) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            project.Data[key] = value;
            _repository.Save(project, true);

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
        public IHttpActionResult DeleteData(string id, string key) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.Data.Remove(key))
                _repository.Save(project, true);

            return Ok();
        }

        protected override void CreateMaps() {
            Mapper.CreateMap<Project, ViewProject>().AfterMap((p, pi) => {
                pi.OrganizationName = _organizationRepository.GetById(p.OrganizationId, true).Name;
            });
            base.CreateMaps();
        }

        protected override PermissionResult CanAdd(Project value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithMessage("Project name is required.");

            if (!IsProjectNameAvailableInternal(value.OrganizationId, value.Name))
                return PermissionResult.DenyWithMessage("A project with this name already exists.");

            if (!_billingManager.CanAddProject(value))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add additional projects.");

            return base.CanAdd(value);
        }

        protected override Project AddModel(Project value) {
            value.NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks;
            value.AddDefaultOwnerNotificationSettings(ExceptionlessUser.Id);
            var project = base.AddModel(value);

            return project;
        }

        protected override PermissionResult CanUpdate(Project original, Delta<UpdateProject> changes) {
            var changed = changes.GetEntity();
            if (changes.ContainsChangedProperty(p => p.Name) && !IsProjectNameAvailableInternal(original.OrganizationId, changed.Name))
                return PermissionResult.DenyWithMessage("A project with this name already exists.");

            return base.CanUpdate(original, changes);
        }

        protected override async Task DeleteModels(ICollection<Project> projects) {
            foreach (var project in projects) {
                await _tokenRepository.RemoveAllByProjectIdsAsync(new[] { project.Id });
                await _webHookRepository.RemoveAllByProjectIdsAsync(new[] { project.Id });
                await _dataHelper.ResetProjectDataAsync(project.Id);
            }

            await base.DeleteModels(projects);
        }

        private ViewProject PopulateProjectStats(ViewProject project) {
            return PopulateProjectStats(new List<ViewProject> { project }).FirstOrDefault();
        }

        private List<ViewProject> PopulateProjectStats(List<ViewProject> projects) {
            if (projects.Count <= 0)
                return projects;

            var organizations = _organizationRepository.GetByIds(projects.Select(p => p.Id).ToArray(), useCache: true);
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < projects.Count; index++) {
                if (index > 0)
                    builder.Append(" OR ");

                var project = projects[index];
                var organization = organizations.FirstOrDefault(o => o.Id == project.Id);
                if (organization != null && organization.RetentionDays > 0)
                    builder.AppendFormat("(project:{0} AND (date:[now/d-{1}d TO now/d+1d}} OR last:[now/d-{1}d TO now/d+1d}}))", project.Id, organization.RetentionDays);
                else
                    builder.AppendFormat("project:{0}", project.Id);
            }

            var result = _stats.GetTermsStats(DateTime.MinValue, DateTime.MaxValue, "project_id", builder.ToString());
            foreach (var project in projects) {
                var projectStats = result.Terms.FirstOrDefault(t => t.Term == project.Id);
                project.EventCount = projectStats != null ? projectStats.Total : 0;
                project.StackCount = projectStats != null ? projectStats.Unique : 0;
            }

            return projects;
        }
    }
}
