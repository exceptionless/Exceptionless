using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Models.Project;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Stats;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "project")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : ExceptionlessApiController {
        private List<Project> _projects;
        private readonly IProjectRepository _projectRepository;
        private readonly DataHelper _dataHelper;
        private readonly OrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public ProjectController(IProjectRepository projectRepository, OrganizationRepository organizationRepository, DataHelper dataHelper, BillingManager billingManager, NotificationSender notificationSender) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _dataHelper = dataHelper;
        }

        [Route]
        [HttpGet]
        public IEnumerable<Project> Get() {
            var projects = _projectRepository.GetByOrganizationIds(Request.GetAssociatedOrganizationIds()).Take(100).ToList();
            
            string userId = Request.GetUserId();
            return projects.Select(p => {
                NotificationSettings settings = p.GetNotificationSettings(userId);
                p.NotificationSettings.Clear();

                if (settings != null)
                    p.NotificationSettings.Add(userId, settings);

                return p;
            });
        }

        [HttpGet]
        [Route("config")]
        [Route("config/{id}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string id = null) {
            // TODO: Only the client should be using .

            if (String.IsNullOrEmpty(id))
                id = User.GetApiKeyProjectId();
            
            if (String.IsNullOrEmpty(id))
                    return NotFound();

            var project = _projectRepository.GetByIdCached(id);
            if (project == null || !Request.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }

        [HttpGet]
        [Route("{id}")]
        public IHttpActionResult Get(string id) {
            if (String.IsNullOrWhiteSpace(id))
                return BadRequest();

            var project = _projectRepository.GetById(id);
            if (project == null || Request.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            string userId = Request.GetUserId();
            NotificationSettings settings = project.GetNotificationSettings(userId);
            project.NotificationSettings.Clear();

            if (settings != null)
                project.NotificationSettings.Add(userId, settings);

            return Ok(project);
        }

        [Route]
        [HttpPost]
        public IHttpActionResult Post(Project value) {
            if (value == null)
                return BadRequest();

            if (String.IsNullOrWhiteSpace(value.OrganizationId) || !Request.IsInOrganization(value.OrganizationId))
                return BadRequest();

            if (!_billingManager.CanAddProject(value))
                return this.PlanLimitReached("Please upgrade your plan to add an additional project.");

            string userId = Request.GetUserId();
            NotificationSettings settings = value.GetNotificationSettings(userId);
            value.NotificationSettings.Clear();
            value.NotificationSettings.Add(userId, settings);

            List<string> apiKeys = value.ApiKeys.Where(key => _projectRepository.GetByApiKey(key) == null).ToList();
            value.ApiKeys = apiKeys.Any()
                ? new HashSet<string>(apiKeys)
                : new HashSet<string> {
                    Guid.NewGuid().ToString("N").ToLower()
                };

            if (String.IsNullOrWhiteSpace(value.TimeZone))
                value.TimeZone = "Central Standard Time";

            value.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), value.DefaultTimeZone()).ToUniversalTime().Ticks;
            value.StackCount = 0;
            value.ErrorCount = 0;
            value.TotalErrorCount = 0;

            Project project = _projectRepository.Add(value, true);

            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);
            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            return Ok(project);
        }

        [Route]
        [HttpPut]
        public IHttpActionResult Put(string id, Project value) {
            return BadRequest();
        }

        [Route]
        [HttpPatch]
        public IHttpActionResult Patch(string id, Project value) {
            // TODO: We need to add support for array item level patching (E.G., API Keys and Promoted tabs).
            return BadRequest();
        }

        [HttpDelete]
        [Route("{id}")]
        public IHttpActionResult Delete(string id) {
            if (String.IsNullOrWhiteSpace(id))
                return BadRequest();

            var value = _projectRepository.GetById(id);
            if (value == null || Request.CanAccessOrganization(value.OrganizationId))
                return BadRequest();

            _projectRepository.Delete(value.Id);

            // Note: The project may not be deleted at this point..
            _organizationRepository.IncrementStats(value.OrganizationId, projectCount: -1);
            _notificationSender.ProjectUpdated(value.OrganizationId, value.Id);

            return Ok();
        }

        [HttpGet]
        [Route("list")]
        public IEnumerable<ProjectInfoModel> List(int page = 1, int pageSize = 100) {
            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            return Get().Skip(skip).Take(pageSize).Select(p => {
                ProjectInfoModel pi = Mapper.Map<Project, ProjectInfoModel>(p);
                pi.OrganizationName = _organizationRepository.GetByIdCached(p.OrganizationId).Name;

                return pi;
            });
        }

        // TODO: Combine this with list method above.
        [HttpGet]
        [Route("organization/{organizationId}")]
        public IHttpActionResult GetByOrganizationId(string organizationId, int page = 1, int pageSize = 10) {
            if (String.IsNullOrEmpty(organizationId) || !Request.CanAccessOrganization(organizationId))
                return NotFound();

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            List<Project> projects = _projectRepository.GetByOrganizationId(organizationId).ToList();
            List<ProjectInfoModel> projectInfos = projects.Skip(skip).Take(pageSize).Select(p => {
                ProjectInfoModel pi = Mapper.Map<Project, ProjectInfoModel>(p);
                pi.OrganizationName = _organizationRepository.GetByIdCached(p.OrganizationId).Name;

                return pi;
            }).ToList();

            var result = new PagedResult<ProjectInfoModel>(projectInfos, projects.Count) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            };

            // TODO: Only return the populated fields (currently all properties are being returned).
            return Ok(result);
        }

        [HttpGet]
        [Route("{projectId}/reset-data")]
        public void ResetData(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return;

            Project project = _projectRepository.GetByIdCached(projectId);
            if (project == null || !Request.CanAccessOrganization(project.OrganizationId))
                return;

            _dataHelper.ResetProjectData(projectId);
        }

        [HttpGet]
        [Route("{projectId}/get-key")]
        public IHttpActionResult GetOrAddKey(string projectId) {
            if (String.IsNullOrWhiteSpace(projectId))
                return BadRequest();

            var project = _projectRepository.GetById(projectId);
            if (project == null || Request.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            if (project.ApiKeys.Count > 0)
                return Ok(project.ApiKeys.First());

            return ManageApiKeys(projectId);
        }

        [HttpPost]
        [Route("{projectId}/key/")]
        public IHttpActionResult ManageApiKeys(string projectId) {
            if (String.IsNullOrWhiteSpace(projectId))
                return BadRequest();

            var project = _projectRepository.GetById(projectId);
            if (project == null || Request.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            string apiKey = Guid.NewGuid().ToString("N").ToLower();
            project.ApiKeys.Add(apiKey);

            _projectRepository.Update(project);

            return Ok(apiKey);
        }

        [HttpDelete]
        [Route("{projectId}/key/{apiKey:?}")]
        public IHttpActionResult ManageApiKeys(string projectId, string apiKey) {
            if (String.IsNullOrWhiteSpace(projectId) || String.IsNullOrEmpty(apiKey))
                return BadRequest();

            var project = _projectRepository.GetById(projectId);
            if (project == null || Request.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            if (!project.ApiKeys.Contains(apiKey))
                return StatusCode(HttpStatusCode.NoContent);

            if (!Request.CanAccessOrganization(project.OrganizationId))
                throw new Exception("Invalid organization.");

            project.ApiKeys.Remove(apiKey);
            _projectRepository.Update(project);

            return Ok();
        }

        // TODO: Move this to use patch.
        [HttpPut]
        [Route("{projectId}/notification/{userId}")]
        public IHttpActionResult Notification(string projectId, string userId, NotificationSettings settings) {
            if (String.IsNullOrEmpty(projectId) || String.IsNullOrEmpty(userId) || settings == null)
                return BadRequest();

            Project project = _projectRepository.GetById(projectId);
            if (project == null || Request.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            project.NotificationSettings[userId] = settings;
            _projectRepository.Update(project);

            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            // Acceptable status codes are 200/201/204
            return Ok();
        }

        //protected override bool CanUpdateEntity(Project original, Delta<Project> value) {
        //    Project entity = value.GetEntity();
        //    if (value.ContainsChangedProperty(t => t.Name)
        //        && !String.IsNullOrEmpty(entity.Name)
        //        && !String.Equals(original.Name, entity.Name, StringComparison.OrdinalIgnoreCase)
        //        && base.Get().Count(p => String.Equals(p.Name, entity.Name, StringComparison.OrdinalIgnoreCase)) > 0)
        //        return false;

        //    if (value.ContainsChangedProperty(t => t.ApiKeys)) {
        //        // If the new patch value contains any api keys that are not in the original api key list then return false.
        //        if (entity.ApiKeys.Any(key => !original.ApiKeys.Contains(key)))
        //            return false;

        //        if (entity.ApiKeys.Any(key => _projectRepository.GetByApiKey(key) != null))
        //            return false;
        //    }

        //    if ((value.ContainsChangedProperty(t => t.StackCount) && original.StackCount != entity.StackCount)
        //        || (value.ContainsChangedProperty(t => t.ErrorCount) && original.ErrorCount != entity.ErrorCount)
        //        || (value.ContainsChangedProperty(t => t.TotalErrorCount) && original.TotalErrorCount != entity.TotalErrorCount))
        //        return false;

        //    //if (value.ContainsChangedProperty(t => t.Configuration))
        //    //    return false;

        //    return base.CanUpdateEntity(original, value);
        //}

        //protected override Project UpdateEntity(Project original, Delta<Project> value) {
        //    int version = original.Configuration.Version;
        //    value.Patch(original);
        //    original.Configuration.Version = version;

        //    Project project = _projectRepository.Update(original);

        //    _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

        //    return project;
        //}

        [HttpGet]
        [Route("is-name-available")]
        public IHttpActionResult IsNameAvailable(string id, string name) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrWhiteSpace(name))
                return Ok(false);

            foreach (Project project in Projects) {
                if (String.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)) {
                    if (String.Equals(project.Id, id, StringComparison.OrdinalIgnoreCase))
                        break;

                    return Ok(false);
                }
            }

            return Ok(true);
        }

        private IEnumerable<Project> Projects {
            get {
                if (User == null)
                    return new List<Project>();

                if (_projects == null)
                    _projects = _projectRepository.GetByOrganizationIds(Request.GetAssociatedOrganizationIds()).ToList();

                return _projects;
            }
        }
    }
}