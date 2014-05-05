using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Models;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "project")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : RepositoryApiController<ProjectRepository, Project, ViewProject, NewProject, UpdateProject> {
        private List<Project> _projects;
        private readonly DataHelper _dataHelper;
        private readonly OrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;

        public ProjectController(ProjectRepository projectRepository, OrganizationRepository organizationRepository, DataHelper dataHelper, BillingManager billingManager)
            : base(projectRepository) {
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
        }

        #region CRUD

        [HttpGet]
        [Route]
        public override IHttpActionResult Get(string organizationId = null, int page = 1, int pageSize = 10) {
            return base.Get(organizationId, page, pageSize);
        }

        [HttpGet]
        [Route("{id}", Name = "GetProjectById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [HttpPost]
        [Route]
        public override IHttpActionResult Post(NewProject value) {
            if (String.IsNullOrEmpty(value.OrganizationId))
                value.OrganizationId = GetDefaultOrganizationId();

            return base.Post(value);
        }

        [HttpPatch]
        [HttpPut]
        [Route("{id}")]
        public override IHttpActionResult Patch(string id, Delta<UpdateProject> changes) {
            return base.Patch(id, changes);
        }

        [HttpDelete]
        [Route("{id}")]
        public override IHttpActionResult Delete(string id) {
            return base.Delete(id);
        }

        #endregion

        [HttpGet]
        [Route("config")]
        [Route("config/{id}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string id = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetProjectId();
            
            if (String.IsNullOrEmpty(id))
                    return NotFound();

            var project = _repository.GetByIdCached(id);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }

        [HttpGet]
        [Route("{id}/reset-data")]
        public IHttpActionResult ResetData(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            Project project = _repository.GetByIdCached(id);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            // TODO: Implement a long running process queue where a task can be inserted and then monitor for progress.
            _dataHelper.ResetProjectData(id);

            return Ok();
        }

        [HttpGet]
        [Route("{id}/get-key")]
        public IHttpActionResult GetOrAddKey(string id) {
            if (String.IsNullOrWhiteSpace(id))
                return BadRequest();

            var project = _repository.GetById(id);
            if (project == null || CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            if (project.ApiKeys.Count > 0)
                return Ok(project.ApiKeys.First());

            return ManageApiKeys(id);
        }

        [HttpPost]
        [Route("{id}/key/")]
        public IHttpActionResult ManageApiKeys(string id) {
            if (String.IsNullOrWhiteSpace(id))
                return BadRequest();

            var project = _repository.GetById(id);
            if (project == null || CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            string apiKey = Guid.NewGuid().ToString("N").ToLower();
            project.ApiKeys.Add(apiKey);

            _repository.Update(project);

            return Ok(apiKey);
        }

        [HttpDelete]
        [Route("{id}/key/{apiKey}")]
        public IHttpActionResult ManageApiKeys(string id, string apiKey) {
            if (String.IsNullOrWhiteSpace(id) || String.IsNullOrEmpty(apiKey))
                return BadRequest();

            var project = _repository.GetById(id);
            if (project == null || CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            if (!project.ApiKeys.Contains(apiKey))
                return StatusCode(HttpStatusCode.NoContent);

            if (!CanAccessOrganization(project.OrganizationId))
                throw new Exception("Invalid organization.");

            project.ApiKeys.Remove(apiKey);
            _repository.Update(project);

            return Ok();
        }

        [HttpPut]
        [Route("{id}/notification/{userId}")]
        public IHttpActionResult Notification(string id, string userId, NotificationSettings settings) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(userId) || settings == null)
                return BadRequest();

            Project project = _repository.GetById(id);
            if (project == null || CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            project.NotificationSettings[userId] = settings;
            _repository.Update(project);

            return Ok();
        }

        [HttpGet]
        [Route("is-name-available")]
        public IHttpActionResult IsNameAvailable(string id, string name) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrWhiteSpace(name))
                return NotFound();

            foreach (Project project in Projects) {
                if (String.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)) {
                    if (String.Equals(project.Id, id, StringComparison.OrdinalIgnoreCase))
                        break;

                    return NotFound();
                }
            }

            return Ok();
        }

        protected override void CreateMaps() {
            Mapper.CreateMap<Project, ViewProject>().AfterMap((p, pi) => {
                pi.TimeZoneOffset = p.DefaultTimeZoneOffset().TotalMilliseconds;
                pi.OrganizationName = _organizationRepository.GetByIdCached(p.OrganizationId).Name;
            });
            base.CreateMaps();
        }

        protected override PermissionResult CanAdd(Project value) {
            if (!_billingManager.CanAddProject(value))
                return PermissionResult.DenyWithResult(PlanLimitReached("Please upgrade your plan to add additional projects."));

            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithResult(BadRequest("Project name is required."));

            return base.CanAdd(value);
        }

        protected override Project AddModel(Project value) {
            if (String.IsNullOrWhiteSpace(value.TimeZone))
                value.TimeZone = TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now) ? TimeZone.CurrentTimeZone.DaylightName : TimeZone.CurrentTimeZone.StandardName;

            value.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), value.DefaultTimeZone()).ToUniversalTime().Ticks;
            var project = base.AddModel(value);

            return project;
        }

        private IEnumerable<Project> Projects {
            get {
                if (User == null)
                    return new List<Project>();

                if (_projects == null)
                    _projects = _repository.GetByOrganizationIds(GetAssociatedOrganizationIds()).ToList();

                return _projects;
            }
        }
    }
}