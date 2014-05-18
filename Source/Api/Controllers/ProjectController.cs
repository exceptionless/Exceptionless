using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/project")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : RepositoryApiController<IProjectRepository, Project, ViewProject, NewProject, UpdateProject> {
        private List<Project> _projects;
        private readonly DataHelper _dataHelper;
        private readonly OrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;

        public ProjectController(IProjectRepository projectRepository, OrganizationRepository organizationRepository, DataHelper dataHelper, BillingManager billingManager) : base(projectRepository) {
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _dataHelper = dataHelper;
        }

        #region CRUD

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string organizationId = null, string before = null, string after = null, int limit = 10) {
            if (!CanAccessOrganization(organizationId))
                return NotFound();

            var options = new PagingOptions { Before = before, After = after, Limit = limit };
            var results = _repository.GetByOrganizationId(organizationId, options);
            return OkWithResourceLinks(results, options.HasMore, e => e.Id);
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetProjectById")]
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
        [Route("{id:objectid}")]
        public override IHttpActionResult Patch(string id, Delta<UpdateProject> changes) {
            return base.Patch(id, changes);
        }

        [HttpDelete]
        [Route("{id:objectid}")]
        public override IHttpActionResult Delete(string id) {
            return base.Delete(id);
        }

        #endregion

        [HttpGet]
        [Route("config")]
        [Route("{id:objectid}/config")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult GetConfig(string id = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetProjectId();

            var project = GetModel(id);
            if (project == null)
                return NotFound();

            return Ok(project.Configuration);
        }

        [HttpPost]
        [Route("{id:objectid}/config/{key}")]
        public IHttpActionResult SetConfig(string id, string key, string value) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            project.Configuration.Settings[key] = value;
            project.Configuration.Version++;
            _repository.Save(project);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/config/{key}")]
        public IHttpActionResult DeleteConfig(string id, string key) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            if (project.Configuration.Settings.Remove(key))
                _repository.Save(project);

            return Ok();
        }

        [HttpGet]
        [Route("{id:objectid}/reset-data")]
        public async Task<IHttpActionResult> ResetDataAsync(string id) {
            var project = GetModel(id);
            if (project == null)
                return BadRequest();

            // TODO: Implement a long running process queue where a task can be inserted and then monitor for progress.
            await _dataHelper.ResetProjectDataAsync(id);

            return Ok();
        }

        [HttpGet]
        [Route("{id:objectid}/apikey/get-default")]
        public IHttpActionResult GetDefaultApiKey(string id) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            if (project.ApiKeys.Count > 0)
                return Ok(new { Key = project.ApiKeys.First() });

            return GetNewApiKey(id);
        }

        [HttpPost]
        [Route("{id:objectid}/apikey/")]
        public IHttpActionResult GetNewApiKey(string id) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            string apiKey = Guid.NewGuid().ToString("N").ToLower();
            project.ApiKeys.Add(apiKey);

            _repository.Save(project);

            return Ok(new { Key = apiKey });
        }

        [HttpDelete]
        [Route("{id:objectid}/apikey/{apiKey}")]
        public IHttpActionResult DeleteApiKey(string id, string apiKey) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            if (!project.ApiKeys.Contains(apiKey))
                return StatusCode(HttpStatusCode.NoContent);

            if (project.ApiKeys.Remove(apiKey))
                _repository.Save(project);

            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpGet]
        [Route("{id:objectid}/notification")]
        public IHttpActionResult GetNotificationSettings(string id) {
            var project = GetModel(id);
            if (project == null)
                return NotFound();

            return Ok(project.NotificationSettings);
        }

        // TODO: Should we remove userId and just use the current user..
        [HttpGet]
        [Route("{id:objectid}/notification/{userId:objectid}")]
        public IHttpActionResult GetNotificationSettings(string id, string userId) {
            var project = GetModel(id);
            if (project == null)
                return NotFound();

            if (!project.NotificationSettings.ContainsKey(userId))
                return NotFound();

            // TODO: We should just return the settings instead of user id and settings.
            return Ok(project.NotificationSettings[userId]);
        }

        // TODO: Should we remove userId and just use the current user..
        [HttpPut]
        [HttpPost]
        [Route("{id:objectid}/notification/{userId:objectid}")]
        public IHttpActionResult SetNotificationSettings(string id, string userId, NotificationSettings settings) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            project.NotificationSettings[userId] = settings;
            _repository.Save(project);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/notification/{userId:objectid}")]
        public IHttpActionResult DeleteNotificationSettings(string id, string userId) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.NotificationSettings.ContainsKey(userId)) {
                project.NotificationSettings.Remove(userId);
                _repository.Save(project);
            }

            return Ok();
        }

        [HttpPut]
        [HttpPost]
        [Route("{id:objectid}/promotedtabs/{name}")]
        public IHttpActionResult PromoteTab(string id, string name) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (!project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Add(name);
                _repository.Save(project);
            }

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/promotedtabs/{name}")]
        public IHttpActionResult DemoteTab(string id, string name) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (!project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Remove(name);
                _repository.Save(project);
            }

            return Ok();
        }

        [HttpGet]
        [Route("check-name")]
        public IHttpActionResult IsNameAvailable(string name) {
            if (String.IsNullOrWhiteSpace(name))
                return NotFound();

            if (_repository.GetByOrganizationIds(GetAssociatedOrganizationIds()).Any(o => o.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Ok();

            return NotFound();
        }

        [HttpPost]
        [Route("{id:objectid}/data/{key}")]
        public IHttpActionResult PostData(string id, string key, string value) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            project.Data[key] = value;
            _repository.Save(project);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/data/{key}")]
        public IHttpActionResult DeleteData(string id, string key) {
            var project = GetModel(id, false);
            if (project == null)
                return BadRequest();

            if (project.Data.Remove(key))
                _repository.Save(project);

            return Ok();
        }

        protected override void CreateMaps() {
            Mapper.CreateMap<Project, ViewProject>().AfterMap((p, pi) => {
                pi.TimeZoneOffset = p.DefaultTimeZoneOffset().TotalMilliseconds;
                pi.OrganizationName = _organizationRepository.GetById(p.OrganizationId, true).Name;
            });
            base.CreateMaps();
        }

        protected override PermissionResult CanAdd(Project value) {
            if (String.IsNullOrEmpty(value.Name))
                return PermissionResult.DenyWithResult(BadRequest("Project name is required."));

            if (!_billingManager.CanAddProject(value))
                return PermissionResult.DenyWithResult(PlanLimitReached("Please upgrade your plan to add additional projects."));

            return base.CanAdd(value);
        }

        protected override Project AddModel(Project value) {
            if (String.IsNullOrWhiteSpace(value.TimeZone))
                value.TimeZone = TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now) ? TimeZone.CurrentTimeZone.DaylightName : TimeZone.CurrentTimeZone.StandardName;

            value.NextSummaryEndOfDayTicks = TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), value.DefaultTimeZone()).ToUniversalTime().Ticks;
            var project = base.AddModel(value);

            return project;
        }
    }
}