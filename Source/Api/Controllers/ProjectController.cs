using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Api.Utility;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/projects")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : RepositoryApiController<IProjectRepository, Project, ViewProject, NewProject, UpdateProject> {
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
        public IHttpActionResult Get(int page = 1, int limit = 10) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByOrganizationIds(GetAssociatedOrganizationIds(), options).Select(Mapper.Map<Project, ViewProject>).ToList();
            return OkWithResourceLinks(results, options.HasMore, page);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/organizations/{organization:objectid}/projects")]
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
            var results = _repository.GetByOrganizationIds(organizationIds, options).Select(Mapper.Map<Project, ViewProject>).ToList();
            return OkWithResourceLinks(results, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
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
        [Route("{ids:objectids}")]
        public override IHttpActionResult Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        #endregion

        [HttpGet]
        [Route("config")]
        [Route("{id:objectid}/config")]
        [Route("~/api/v1/project/config")]
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
        [Route("{id:objectid}/config/{key:minlength(1)}")]
        public IHttpActionResult SetConfig(string id, string key, [NakedBody] string value) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (String.IsNullOrWhiteSpace(value))
                return BadRequest();

            project.Configuration.Settings[key] = value;
            project.Configuration.IncrementVersion();
            _repository.Save(project);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/config/{key:minlength(1)}")]
        public IHttpActionResult DeleteConfig(string id, string key) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.Configuration.Settings.Remove(key))
                _repository.Save(project);

            return Ok();
        }

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
        public IHttpActionResult GetNotificationSettings(string id) {
            var project = GetModel(id);
            if (project == null)
                return NotFound();

            return Ok(project.NotificationSettings);
        }

        [HttpGet]
        [Route("{id:objectid}/notifications/{userId:objectid}")]
        public IHttpActionResult GetNotificationSettings(string id, string userId) {
            var project = GetModel(id);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
                return NotFound();

            NotificationSettings settings;
            return Ok(project.NotificationSettings.TryGetValue(userId, out settings) ? settings : new NotificationSettings());
        }

        [HttpPut]
        [HttpPost]
        [Route("{id:objectid}/notifications/{userId:objectid}")]
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

            _repository.Save(project);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/notifications/{userId:objectid}")]
        public IHttpActionResult DeleteNotificationSettings(string id, string userId) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(ExceptionlessUser.Id, userId))
                return NotFound();

            if (project.NotificationSettings.ContainsKey(userId)) {
                project.NotificationSettings.Remove(userId);
                _repository.Save(project);
            }

            return Ok();
        }

        [HttpPut]
        [HttpPost]
        [Route("{id:objectid}/promotedtabs/{name:minlength(1)}")]
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
        [Route("{id:objectid}/promotedtabs/{name:minlength(1)}")]
        public IHttpActionResult DemoteTab(string id, string name) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.PromotedTabs.Contains(name)) {
                project.PromotedTabs.Remove(name);
                _repository.Save(project);
            }

            return Ok();
        }

        [HttpGet]
        [Route("check-name/{name:minlength(1)}")]
        public IHttpActionResult IsNameAvailable(string name) {
            if (IsNameAvailableInternal(name))
                return NotFound();

            return Ok();
        }

        private bool IsNameAvailableInternal(string name) {
            return !String.IsNullOrWhiteSpace(name) && _repository.GetByIds(GetAssociatedOrganizationIds()).Any(o => o.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        [HttpPost]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public IHttpActionResult PostData(string id, string key, string value) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            project.Data[key] = value;
            _repository.Save(project);

            return Ok();
        }

        [HttpDelete]
        [Route("{id:objectid}/data/{key:minlength(1)}")]
        public IHttpActionResult DeleteData(string id, string key) {
            var project = GetModel(id, false);
            if (project == null)
                return NotFound();

            if (project.Data.Remove(key))
                _repository.Save(project);

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

            if (!IsNameAvailableInternal(value.Name))
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
            if (changes.ContainsChangedProperty(p => p.Name) && !IsNameAvailableInternal(changed.Name))
                return PermissionResult.DenyWithPlanLimitReached("A project with this name already exists.");

            return base.CanUpdate(original, changes);
        }
    }
}
