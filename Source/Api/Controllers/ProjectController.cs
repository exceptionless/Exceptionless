using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Models;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "project")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : OwnedByOrganizationRepositoryApiController<Project, ViewProject, NewProject, ProjectRepository> {
        private List<Project> _projects;
        private readonly DataHelper _dataHelper;
        private readonly OrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public ProjectController(ProjectRepository projectRepository, OrganizationRepository organizationRepository, DataHelper dataHelper, BillingManager billingManager, NotificationSender notificationSender) : base(projectRepository) {
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _dataHelper = dataHelper;
        }

        protected override void CreateMaps() {
            Mapper.CreateMap<NewProject, Project>();
            Mapper.CreateMap<Project, ViewProject>().AfterMap((p, pi) => {
                pi.TimeZoneOffset = p.DefaultTimeZoneOffset().TotalMilliseconds;
                pi.OrganizationName = _organizationRepository.GetByIdCached(p.OrganizationId).Name;
            });
        }

        protected override PermissionResult CanAdd(Project value) {
            if (String.IsNullOrWhiteSpace(value.OrganizationId) || !User.IsInOrganization(value.OrganizationId))
                return PermissionResult.Deny;

            if (!_billingManager.CanAddProject(value))
                return new PermissionResult {
                    Allowed = false,
                    HttpActionResult = PlanLimitReached("Please upgrade your plan to add an additional project.")
                };

            if (String.IsNullOrEmpty(value.Name))
                return new PermissionResult {
                    Allowed = false,
                    HttpActionResult = BadRequest("Invalid project name")
                };

            return base.CanAdd(value);
        }

        protected override Project AddModel(Project value) {
            if (String.IsNullOrWhiteSpace(value.TimeZone))
                value.TimeZone = "Central Standard Time";

            value.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), value.DefaultTimeZone()).ToUniversalTime().Ticks;
            Project project = _repository.Add(value, true);

            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);

            // TODO: Send a message and have SignalR pick it up later.
            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            return project;
        }

        protected virtual string[] GetUpdatablePropertyNames() {
            return new string[] { "Name", "CustomContent" };
        }

        protected override Project UpdateModel(Project original, Delta<Project> changes) {
            Project project = base.UpdateModel(original, changes);

            // TODO: Send a message and have SignalR pick it up later.
            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);
            return project;
        }

        protected override void DeleteModel(Project value) {
            base.DeleteModel(value);

            // Note: The project may not be deleted at this point..
            _organizationRepository.IncrementStats(value.OrganizationId, projectCount: -1);

            // TODO: Send a message and have SignalR pick it up later.
            _notificationSender.ProjectUpdated(value.OrganizationId, value.Id);
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

            var project = _repository.GetByIdCached(id);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }

        [Route]
        [HttpGet]
        public IHttpActionResult Get(string organizationId = null, int page = 1, int pageSize = 10) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                return NotFound();

            var query = Query.In("oid", Request.GetAssociatedOrganizationIds().Select(id => new BsonObjectId(new ObjectId(id))));
            var results = GetEntities<ViewProject>(query, page: page, pageSize: pageSize);
            return Ok(new PagedResult<ViewProject>(results) {
                Page = page > 1 ? page : 1,
                PageSize = pageSize >= 1 ? pageSize : 10
            });
        }

        // TODO: Combine this with list method above.
        [HttpGet]
        [Route("organization/{organizationId}")]
        public IHttpActionResult GetByOrganizationId(string organizationId, int page = 1, int pageSize = 10) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                return NotFound();

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            List<Project> projects = _repository.GetByOrganizationId(organizationId).ToList();
            List<ViewProject> projectInfos = projects.Skip(skip).Take(pageSize).Select(p => {
                ViewProject pi = Mapper.Map<Project, ViewProject>(p);
                pi.OrganizationName = _organizationRepository.GetByIdCached(p.OrganizationId).Name;

                return pi;
            }).ToList();

            var result = new PagedResult<ViewProject>(projectInfos, projects.Count) {
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

            Project project = _repository.GetByIdCached(projectId);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return;

            _dataHelper.ResetProjectData(projectId);
        }

        [HttpGet]
        [Route("{projectId}/get-key")]
        public IHttpActionResult GetOrAddKey(string projectId) {
            if (String.IsNullOrWhiteSpace(projectId))
                return BadRequest();

            var project = _repository.GetById(projectId);
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
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

            var project = _repository.GetById(projectId);
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            string apiKey = Guid.NewGuid().ToString("N").ToLower();
            project.ApiKeys.Add(apiKey);

            _repository.Update(project);

            return Ok(apiKey);
        }

        [HttpDelete]
        [Route("{projectId}/key/{apiKey:?}")]
        public IHttpActionResult ManageApiKeys(string projectId, string apiKey) {
            if (String.IsNullOrWhiteSpace(projectId) || String.IsNullOrEmpty(apiKey))
                return BadRequest();

            var project = _repository.GetById(projectId);
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            if (!project.ApiKeys.Contains(apiKey))
                return StatusCode(HttpStatusCode.NoContent);

            if (!User.CanAccessOrganization(project.OrganizationId))
                throw new Exception("Invalid organization.");

            project.ApiKeys.Remove(apiKey);
            _repository.Update(project);

            return Ok();
        }

        // TODO: Move this to use patch.
        [HttpPut]
        [Route("{projectId}/notification/{userId}")]
        public IHttpActionResult Notification(string projectId, string userId, NotificationSettings settings) {
            if (String.IsNullOrEmpty(projectId) || String.IsNullOrEmpty(userId) || settings == null)
                return BadRequest();

            Project project = _repository.GetById(projectId);
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            project.NotificationSettings[userId] = settings;
            _repository.Update(project);

            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            // Acceptable status codes are 200/201/204
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

        private IEnumerable<Project> Projects {
            get {
                if (User == null)
                    return new List<Project>();

                if (_projects == null)
                    _projects = _repository.GetByOrganizationIds(Request.GetAssociatedOrganizationIds()).ToList();

                return _projects;
            }
        }
    }
}