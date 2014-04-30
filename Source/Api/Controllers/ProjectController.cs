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
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
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
        private readonly IMessagePublisher _messagePublisher;

        public ProjectController(ProjectRepository projectRepository, OrganizationRepository organizationRepository, DataHelper dataHelper, BillingManager billingManager, IMessagePublisher messagePublisher)
            : base(projectRepository) {
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _messagePublisher = messagePublisher;
            _dataHelper = dataHelper;
        }

        [HttpGet]
        [Route]
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

        [HttpGet]
        [Route("{id}")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [HttpPost]
        [Route]
        public override IHttpActionResult Post(NewProject value) {
            return base.Post(value);
        }

        [HttpPatch]
        [HttpPut]
        [Route("{id}")]
        public override IHttpActionResult Patch(string id, Delta<Project> changes) {
            return base.Patch(id, changes);
        }

        [HttpDelete]
        [Route("{id}")]
        public override IHttpActionResult Delete(string id) {
            return base.Delete(id);
        }

        [HttpGet]
        [Route("config")]
        [Route("config/{id}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string id = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetApiKeyProjectId();
            
            if (String.IsNullOrEmpty(id))
                    return NotFound();

            var project = _repository.GetByIdCached(id);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }

        [HttpGet]
        [Route("{id}/reset-data")]
        public IHttpActionResult ResetData(string id) {
            if (String.IsNullOrEmpty(id))
                return BadRequest();

            Project project = _repository.GetByIdCached(id);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            _dataHelper.ResetProjectData(id);

            return Ok();
        }

        [HttpGet]
        [Route("{id}/get-key")]
        public IHttpActionResult GetOrAddKey(string id) {
            if (String.IsNullOrWhiteSpace(id))
                return BadRequest();

            var project = _repository.GetById(id);
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
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
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            string apiKey = Guid.NewGuid().ToString("N").ToLower();
            project.ApiKeys.Add(apiKey);

            _repository.Update(project);

            return Ok(apiKey);
        }

        [HttpDelete]
        [Route("{id}/key/{apiKey:?}")]
        public IHttpActionResult ManageApiKeys(string id, string apiKey) {
            if (String.IsNullOrWhiteSpace(id) || String.IsNullOrEmpty(apiKey))
                return BadRequest();

            var project = _repository.GetById(id);
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

        [HttpPut]
        [Route("{id}/notification/{userId}")]
        public IHttpActionResult Notification(string id, string userId, NotificationSettings settings) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(userId) || settings == null)
                return BadRequest();

            Project project = _repository.GetById(id);
            if (project == null || User.CanAccessOrganization(project.OrganizationId))
                return BadRequest();

            project.NotificationSettings[userId] = settings;
            _repository.Update(project);

            _messagePublisher.PublishAsync(new ProjectChange {
                Id = project.Id,
                OrganizationId = project.OrganizationId,
                IsNew = false
            });

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
                return PermissionResult.DenyWithResult(PlanLimitReached("Please upgrade your plan to add an additional project."));

            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithResult(BadRequest("Invalid project name"));

            return base.CanAdd(value);
        }

        protected override Project AddModel(Project value) {
            if (String.IsNullOrWhiteSpace(value.TimeZone))
                value.TimeZone = "Central Standard Time";

            value.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), value.DefaultTimeZone()).ToUniversalTime().Ticks;
            Project project = _repository.Add(value, true);

            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);
            _messagePublisher.PublishAsync(new ProjectChange {
                Id = project.Id,
                OrganizationId = project.OrganizationId,
                IsNew = true
            });

            return project;
        }

        protected override IEnumerable<string> GetUpdatablePropertyNames() {
            return new[] { "Name", "CustomContent" };
        }

        protected override Project UpdateModel(Project original, Delta<Project> changes) {
            Project project = base.UpdateModel(original, changes);

            _messagePublisher.PublishAsync(new ProjectChange {
                Id = project.Id,
                OrganizationId = project.OrganizationId,
                IsNew = false
            });
            return project;
        }

        protected override void DeleteModel(Project value) {
            base.DeleteModel(value);

            // Note: The project may not be deleted at this point..
            _organizationRepository.IncrementStats(value.OrganizationId, projectCount: -1);

            _messagePublisher.PublishAsync(new ProjectChange {
                Id = value.Id,
                OrganizationId = value.OrganizationId,
                IsNew = true
            });
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