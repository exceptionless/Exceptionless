#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using AutoMapper;
using Exceptionless.App.Hubs;
using Exceptionless.App.Models.Project;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Web;
using Exceptionless.Core.Web.OData;
using Exceptionless.Models;
using Exceptionless.Models.Stats;

namespace Exceptionless.App.Controllers.API {
    [ConfigurationResponseFilter]
    public class ProjectController : RepositoryOwnedByOrganizationApiController<Project, IProjectRepository> {
        private readonly DataHelper _dataHelper;
        private readonly OrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public ProjectController(IProjectRepository repository,
            OrganizationRepository organizationRepository,
            DataHelper dataHelper,
            BillingManager billingManager,
            NotificationSender notificationSender) : base(repository) {
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
            _dataHelper = dataHelper;
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public override IEnumerable<Project> Get() {
            return base.Get().Select(p => {
                NotificationSettings settings = p.GetNotificationSettings(User.UserEntity.Id);
                p.NotificationSettings.Clear();

                if (settings != null)
                    p.NotificationSettings.Add(User.UserEntity.Id, settings);

                return p;
            });
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public override Project Get(string id) {
            Project project = base.Get(id);
            NotificationSettings settings = project.GetNotificationSettings(User.UserEntity.Id);
            project.NotificationSettings.Clear();

            if (settings != null)
                project.NotificationSettings.Add(User.UserEntity.Id, settings);

            return project;
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Post(Project value) {
            return base.Post(value);
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Put(string id, Delta<Project> value) {
            return base.Put(id, value);
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Patch(string id, Delta<Project> value) {
            // TODO: We need to add support for array item level patching (E.G., API Keys and Promoted tabs).
            return base.Patch(id, value);
        }

        [Authorize(Roles = AuthorizationRoles.User)]
        public override HttpResponseMessage Delete(string id) {
            return base.Delete(id);
        }

        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IEnumerable<ProjectInfoModel> List(int page = 1, int pageSize = 100) {
            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            return base.Get().Skip(skip).Take(pageSize).Select(p => {
                ProjectInfoModel pi = Mapper.Map<Project, ProjectInfoModel>(p);
                pi.OrganizationName = _organizationRepository.GetByIdCached(p.OrganizationId).Name;

                return pi;
            });
        }

        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult GetByOrganizationId(string organizationId, int page = 1, int pageSize = 10) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                return NotFound();

            pageSize = GetPageSize(pageSize);
            int skip = GetSkip(page, pageSize);

            List<Project> projects = _repository.GetByOrganizationId(organizationId).ToList();
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

        protected override Project InsertEntity(Project value) {
            if (value == null)
                throw new ArgumentNullException("value");

            if (String.IsNullOrWhiteSpace(value.OrganizationId) || !User.IsInOrganization(value.OrganizationId))
                throw new HttpResponseException(InvalidOrganizationErrorResponseMessage());

            if (!_billingManager.CanAddProject(value))
                throw new HttpResponseException(PlanLimitReached("Please upgrade your plan to add an additional project."));

            NotificationSettings settings = value.GetNotificationSettings(User.UserEntity.Id);
            value.NotificationSettings.Clear();
            value.NotificationSettings.Add(User.UserEntity.Id, settings);

            List<string> apiKeys = value.ApiKeys.Where(key => _repository.GetByApiKey(key) == null).ToList();
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

            Project project = base.InsertEntity(value);

            _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);
            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            return project;
        }

        protected override void DeleteEntity(Project value) {
            base.DeleteEntity(value);

            // Note: The project may not be deleted at this point..
            _organizationRepository.IncrementStats(value.OrganizationId, projectCount: -1);

            _notificationSender.ProjectUpdated(value.OrganizationId, value.Id);
        }

        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.User)]
        public string GetOrAddKey(string projectId) {
            Project project = Get(projectId);
            if (project.ApiKeys.Count > 0)
                return project.ApiKeys.FirstOrDefault();

            return ManageApiKeys(projectId);
        }

        [HttpPost]
        [Authorize(Roles = AuthorizationRoles.User)]
        public string ManageApiKeys(string projectId) {
            string apiKey = Guid.NewGuid().ToString("N").ToLower();

            Project project = Get(projectId);
            project.ApiKeys.Add(apiKey);

            _repository.Update(project);

            return apiKey;
        }

        [HttpDelete]
        [Authorize(Roles = AuthorizationRoles.User)]
        public HttpResponseMessage ManageApiKeys(string projectId, string apiKey) {
            if (String.IsNullOrEmpty(apiKey))
                return BadRequestErrorResponseMessage();

            Project project = Get(projectId);
            if (!project.ApiKeys.Contains(apiKey))
                return new HttpResponseMessage(HttpStatusCode.NoContent);

            if (!User.CanAccessOrganization(project.OrganizationId))
                throw new Exception("Invalid organization.");

            project.ApiKeys.Remove(apiKey);

            _repository.Update(project);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Gets the Configuration settings for a specific project.
        /// </summary>
        /// <param name="id">The Project Id</param>
        /// <returns>The Configuration for a specific project.</returns>
        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string id = null) {
            // TODO: Only the client should be using this..
            if (User.Identity.AuthenticationType.Equals("ApiKey"))
                return Ok(User.Project.Configuration);

            if (String.IsNullOrEmpty(id))
                return NotFound();

            Project project = _repository.GetById(id);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }

        [HttpGet]
        [Authorize(Roles = AuthorizationRoles.User)]
        public void ResetData(string id) {
            if (String.IsNullOrEmpty(id))
                return;

            Project project = _repository.GetByIdCached(id);
            if (project == null || !User.CanAccessOrganization(project.OrganizationId))
                return;

            _dataHelper.ResetProjectData(id);
        }

        // TODO: Move this to use patch.
        [HttpPut]
        [Authorize(Roles = AuthorizationRoles.User)]
        public HttpResponseMessage Notification(string id, string userId, NotificationSettings settings) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(userId) || settings == null)
                return BadRequestErrorResponseMessage();

            Project project = _repository.GetById(id);
            if (project == null)
                throw new HttpResponseException(NotFoundErrorResponseMessage(id));

            project.NotificationSettings[userId] = settings;

            _repository.Update(project);

            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            // Acceptable status codes are 200/201/204
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override bool CanUpdateEntity(Project original, Delta<Project> value) {
            Project entity = value.GetEntity();
            if (value.ContainsChangedProperty(t => t.Name)
                && !String.IsNullOrEmpty(entity.Name)
                && !String.Equals(original.Name, entity.Name, StringComparison.OrdinalIgnoreCase)
                && base.Get().Count(p => String.Equals(p.Name, entity.Name, StringComparison.OrdinalIgnoreCase)) > 0)
                return false;

            if (value.ContainsChangedProperty(t => t.ApiKeys)) {
                // If the new patch value contains any api keys that are not in the original api key list then return false.
                if (entity.ApiKeys.Any(key => !original.ApiKeys.Contains(key)))
                    return false;

                if (entity.ApiKeys.Any(key => _repository.GetByApiKey(key) != null))
                    return false;
            }

            if ((value.ContainsChangedProperty(t => t.StackCount) && original.StackCount != entity.StackCount)
                || (value.ContainsChangedProperty(t => t.ErrorCount) && original.ErrorCount != entity.ErrorCount)
                || (value.ContainsChangedProperty(t => t.TotalErrorCount) && original.TotalErrorCount != entity.TotalErrorCount))
                return false;

            //if (value.ContainsChangedProperty(t => t.Configuration))
            //    return false;

            return base.CanUpdateEntity(original, value);
        }

        protected override Project UpdateEntity(Project original, Delta<Project> value) {
            int version = original.Configuration.Version;
            value.Patch(original);
            original.Configuration.Version = version;

            Project project = _repository.Update(original);

            _notificationSender.ProjectUpdated(project.OrganizationId, project.Id);

            return project;
        }
    }
}