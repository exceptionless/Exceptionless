using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Controllers {
    [Route(API_PREFIX + "/projects")]
    [Authorize(Policy = AuthorizationRoles.ClientPolicy)]
    public class ProjectController : RepositoryApiController<IProjectRepository, Project, ViewProject, NewProject, UpdateProject> {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly BillingManager _billingManager;
        private readonly SlackService _slackService;

        public ProjectController(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IEventRepository eventRepository, IQueue<WorkItemData> workItemQueue, BillingManager billingManager, SlackService slackService, IMapper mapper, IQueryValidator validator, ILoggerFactory loggerFactory) : base(projectRepository, mapper, validator, loggerFactory) {
            _organizationRepository = organizationRepository;
            _eventRepository = eventRepository;
            _workItemQueue = workItemQueue;
            _billingManager = billingManager;
            _slackService = slackService;
        }

        #region CRUD

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <param name="mode">If no mode is set then the a light weight project object will be returned. If the mode is set to stats than the fully populated object will be returned.</param>
        [HttpGet]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<ViewProject>))]
        public async Task<IActionResult> GetAsync([FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] string mode = null) {
            page = GetPage(page);
            limit = GetLimit(limit);
            var projects = await _repository.GetByOrganizationIdsAsync(GetAssociatedOrganizationIds(), o => o.PageNumber(page).PageLimit(limit));
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
        [HttpGet("~/" + API_PREFIX + "/organizations/{organization:objectid}/projects")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<ViewProject>))]
        public async Task<IActionResult> GetByOrganizationAsync(string organization, [FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] string mode = null) {
            if (String.IsNullOrEmpty(organization) || !CanAccessOrganization(organization))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var projects = await _repository.GetByOrganizationIdAsync(organization, o => o.PageNumber(page).PageLimit(limit));
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
        [HttpGet("{id:objectid}", Name = "GetProjectById")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewProject))]
        public async Task<IActionResult> GetByIdAsync(string id, [FromQuery] string mode = null) {
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
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status201Created, Type = typeof(ViewProject))]
        public Task<IActionResult> PostAsync([FromBody] NewProject project) {
            return PostImplAsync(project);
        }

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="changes">The changes</param>
        /// <response code="400">An error occurred while updating the project.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpPatch("{id:objectid}")]
        [HttpPut("{id:objectid}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ViewProject))]
        public Task<IActionResult> PatchAsync(string id, [FromBody] Delta<UpdateProject> changes) {
            return PatchImplAsync(id, changes);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of project identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more projects were not found.</response>
        /// <response code="500">An error occurred while deleting one or more projects.</response>
        [HttpDelete("{ids:objectids}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public Task<IActionResult> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        #endregion

        /// <summary>
        /// Get configuration settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="v">The client configuration version.</param>
        /// <response code="304">The client configuration version is the current version.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpGet("config")]
        [HttpGet("{id:objectid}/config")]
        [HttpGet("~/api/v1/project/config")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(ClientConfiguration))]
        public async Task<IActionResult> GetConfigAsync(string id = null, [FromQuery] int? v = null) {
            if (String.IsNullOrEmpty(id))
                id = User.GetProjectId();

            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            if (v.HasValue && v == project.Configuration.Version)
                return StatusCode(StatusCodes.Status304NotModified);

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
        [HttpPost("{id:objectid}/config")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> SetConfigAsync(string id, [FromQuery] string key, [FromBody] string value) {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            project.Configuration.Settings[key.Trim()] = value.Trim();
            project.Configuration.IncrementVersion();
            await _repository.SaveAsync(project, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Remove configuration value
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the configuration object.</param>
        /// <response code="400">Invalid key value.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete("{id:objectid}/config")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteConfigAsync(string id, [FromQuery] string key) {
            if (String.IsNullOrWhiteSpace(key))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.Configuration.Settings.Remove(key.Trim())) {
                project.Configuration.IncrementVersion();
                await _repository.SaveAsync(project, o => o.Cache());
            }

            return Ok();
        }

        /// <summary>
        /// Reset project data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet("{id:objectid}/reset-data")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public async Task<IActionResult> ResetDataAsync(string id) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            string workItemId = await _workItemQueue.EnqueueAsync(new RemoveProjectWorkItem {
                OrganizationId = project.OrganizationId,
                ProjectId = project.Id,
                Reset = true
            });

            return WorkInProgress(new [] { workItemId });
        }

        [HttpGet("{id:objectid}/notifications")]
        [Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> GetNotificationSettingsAsync(string id) {
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
        [HttpGet("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(NotificationSettings))]
        public async Task<IActionResult> GetNotificationSettingsAsync(string id, string userId) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(CurrentUser.Id, userId))
                return NotFound();

            return Ok(project.NotificationSettings.TryGetValue(userId, out var settings) ? settings : new NotificationSettings());
        }


        /// <summary>
        /// Get an integrations notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="integration">The identifier of the integration.</param>
        /// <response code="404">The project or integration could not be found.</response>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{id:objectid}/{integration:minlength(1)}/notifications")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(NotificationSettings))]
        public async Task<IActionResult> GetIntegrationNotificationSettingsAsync(string id, string integration) {
            var project = await GetModelAsync(id);
            if (project == null)
                return NotFound();

            if (!String.Equals(Project.NotificationIntegrations.Slack, integration))
                return NotFound();

            return Ok(project.NotificationSettings.TryGetValue(Project.NotificationIntegrations.Slack, out var settings) ? settings : new NotificationSettings());
        }

        /// <summary>
        /// Set user notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="userId">The identifier of the user.</param>
        /// <param name="settings">The notification settings.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpPut("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        [HttpPost("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> SetNotificationSettingsAsync(string id, string userId, [FromBody] NotificationSettings settings) {
            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(CurrentUser.Id, userId))
                return NotFound();

            if (settings == null)
                project.NotificationSettings.Remove(userId);
            else
                project.NotificationSettings[userId] = settings;

            await _repository.SaveAsync(project, o => o.Cache());
            return Ok();
        }

        /// <summary>
        /// Set an integrations notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="integration">The identifier of the user.</param>
        /// <param name="settings">The notification settings.</param>
        /// <response code="404">The project or integration could not be found.</response>
        /// <response code="426">Please upgrade your plan to enable integrations.</response>
        [HttpPut("{id:objectid}/{integration:minlength(1)}/notifications")]
        [HttpPost("{id:objectid}/{integration:minlength(1)}/notifications")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> SetIntegrationNotificationSettingsAsync(string id, string integration, [FromBody] NotificationSettings settings) {
            if (!String.Equals(Project.NotificationIntegrations.Slack, integration))
                return NotFound();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
            if (organization == null)
                return NotFound();

            if (!organization.HasPremiumFeatures)
                return PlanLimitReached($"Please upgrade your plan to enable {integration.TrimStart('@')} integration.");

            if (settings == null)
                project.NotificationSettings.Remove(integration);
            else
                project.NotificationSettings[integration] = settings;

            await _repository.SaveAsync(project, o => o.Cache());
            return Ok();
        }

        /// <summary>
        /// Remove user notification settings
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="userId">The identifier of the user.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete("~/" + API_PREFIX + "/users/{userId:objectid}/projects/{id:objectid}/notifications")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteNotificationSettingsAsync(string id, string userId) {
            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (!Request.IsGlobalAdmin() && !String.Equals(CurrentUser.Id, userId))
                return NotFound();

            if (project.NotificationSettings.ContainsKey(userId)) {
                project.NotificationSettings.Remove(userId);
                await _repository.SaveAsync(project, o => o.Cache());
            }

            return Ok();
        }

        /// <summary>
        /// Promote tab
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="name">The tab name.</param>
        /// <response code="400">Invalid tab name.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpPut("{id:objectid}/promotedtabs")]
        [HttpPost("{id:objectid}/promotedtabs")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> PromoteTabAsync(string id, [FromQuery] string name) {
            if (String.IsNullOrWhiteSpace(name))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (!project.PromotedTabs.Contains(name.Trim())) {
                project.PromotedTabs.Add(name.Trim());
                await _repository.SaveAsync(project, o => o.Cache());
            }

            return Ok();
        }

        /// <summary>
        /// Demote tab
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="name">The tab name.</param>
        /// <response code="400">Invalid tab name.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete("{id:objectid}/promotedtabs")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> DemoteTabAsync(string id, [FromQuery] string name) {
            if (String.IsNullOrWhiteSpace(name))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.PromotedTabs.Contains(name.Trim())) {
                project.PromotedTabs.Remove(name.Trim());
                await _repository.SaveAsync(project, o => o.Cache());
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
        [HttpGet("check-name")]
        [HttpGet("~/" + API_PREFIX + "/organizations/{organizationId:objectid}/projects/check-name")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status201Created)]
        public async Task<IActionResult> IsNameAvailableAsync([FromQuery] string name, string organizationId = null) {
            if (await IsProjectNameAvailableInternalAsync(organizationId, name))
                return StatusCode(StatusCodes.Status204NoContent);

            return StatusCode(StatusCodes.Status201Created);
        }

        private async Task<bool> IsProjectNameAvailableInternalAsync(string organizationId, string name) {
            if (String.IsNullOrWhiteSpace(name))
                return false;

            var organizationIds = IsInOrganization(organizationId) ? new List<string> { organizationId } : GetAssociatedOrganizationIds();
            var projects = await _repository.GetByOrganizationIdsAsync(organizationIds);

            string decodedName = Uri.UnescapeDataString(name).Trim().ToLowerInvariant();
            return !projects.Documents.Any(p => String.Equals(p.Name.Trim().ToLowerInvariant(), decodedName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Add custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <param name="value">Any string value.</param>
        /// <response code="400">Invalid key or value.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpPost("{id:objectid}/data")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> PostDataAsync(string id, [FromQuery] string key, [FromBody] string value) {
            if (String.IsNullOrWhiteSpace(key) || String.IsNullOrWhiteSpace(value) || key.StartsWith("-"))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            project.Data[key.Trim()] = value.Trim();
            await _repository.SaveAsync(project, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Remove custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="key">The key name of the data object.</param>
        /// <response code="400">Invalid key or value.</response>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete("{id:objectid}/data")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteDataAsync(string id, [FromQuery] string key) {
            if (String.IsNullOrWhiteSpace(key) || key.StartsWith("-"))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.Data.Remove(key.Trim()))
                await _repository.SaveAsync(project, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Adds slack integration to the project
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <param name="code">The oauth code that must be exchanged for an auth token.</param>D
        /// <response code="400">Invalid code or error contacting slack.</response>
        /// <response code="404">The project could not be found.</response>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{id:objectid}/slack")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public async Task<IActionResult> AddSlackAsync(string id, [FromQuery] string code) {
            if (String.IsNullOrWhiteSpace(code))
                return BadRequest();

            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            if (project.Data.ContainsKey(Project.KnownDataKeys.SlackToken))
                return StatusCode(StatusCodes.Status304NotModified);

            SlackToken token = null;
            try {
                token = await _slackService.GetAccessTokenAsync(code);
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id).Property("Code", code).Tag("Slack").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                    _logger.LogError(ex, "Error getting slack access token: {Message}", ex.Message);
            }

            if (token == null)
                return BadRequest();

            project.AddDefaultNotificationSettings(Project.NotificationIntegrations.Slack);
            project.Data[Project.KnownDataKeys.SlackToken] = token;
            await _repository.SaveAsync(project, o => o.Cache());

            return Ok();
        }

        /// <summary>
        /// Remove custom data
        /// </summary>
        /// <param name="id">The identifier of the project.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpDelete("{id:objectid}/slack")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> RemoveSlackAsync(string id) {
            var project = await GetModelAsync(id, false);
            if (project == null)
                return NotFound();

            var token = project.GetSlackToken();
            if (token != null) {
                try {
                    await _slackService.RevokeAccessTokenAsync(token.AccessToken);
                } catch (Exception ex) {
                    using (_logger.BeginScope(new ExceptionlessState().Property("Token", token).Tag("Slack").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetHttpContext(HttpContext)))
                        _logger.LogError(ex, "Error revoking slack access token: {Message}", ex.Message);
                }
            }

            if (project.NotificationSettings.Remove(Project.NotificationIntegrations.Slack) | project.Data.Remove(Project.KnownDataKeys.SlackToken))
                await _repository.SaveAsync(project, o => o.Cache());

            return Ok();
        }

        protected override async Task AfterResultMapAsync<TDestination>(ICollection<TDestination> models) {
            await base.AfterResultMapAsync(models);

            // TODO: We can optimize this by normalizing the project model to include the organization name.
            var viewProjects = models.OfType<ViewProject>().ToList();
            var organizations = await _organizationRepository.GetByIdsAsync(viewProjects.Select(p => p.OrganizationId).ToArray(), o => o.Cache());
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
            value.AddDefaultNotificationSettings(CurrentUser.Id);
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
                    OrganizationId = project.OrganizationId,
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

            var organizations = await _organizationRepository.GetByIdsAsync(viewProjects.Select(p => p.OrganizationId).ToArray(), o => o.Cache());
            var projects = viewProjects.Select(p => new Project { Id = p.Id, CreatedUtc = p.CreatedUtc, OrganizationId = p.OrganizationId }).ToList();
            var sf = new ExceptionlessSystemFilter(projects, organizations);
            var systemFilter = new RepositoryQuery<PersistentEvent>().SystemFilter(sf).DateRange(organizations.GetRetentionUtcCutoff(), SystemClock.UtcNow, (PersistentEvent e) => e.Date).Index(organizations.GetRetentionUtcCutoff(), SystemClock.UtcNow);
            var result = await _eventRepository.CountBySearchAsync(systemFilter, null, $"terms:(project_id~{viewProjects.Count} cardinality:stack_id)");
            foreach (var project in viewProjects) {
                var term = result.Aggregations.Terms<string>("terms_project_id")?.Buckets.FirstOrDefault(t => t.Key == project.Id);
                project.EventCount = term?.Total ?? 0;
                project.StackCount = (long)(term?.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0);
            }

            return viewProjects;
        }
    }
}
