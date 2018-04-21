using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queries.Validation;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.App.Controllers.API {
    [Route(API_PREFIX + "/webhooks")]
    [Authorize(Policy = AuthorizationRoles.ClientPolicy)]
    public class WebHookController : RepositoryApiController<IWebHookRepository, WebHook, WebHook, NewWebHook, NewWebHook> {
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;

        public WebHookController(IWebHookRepository repository, IProjectRepository projectRepository, BillingManager billingManager, IMapper mapper, IQueryValidator validator, ILoggerFactory loggerFactory) : base(repository, mapper, validator, loggerFactory) {
            _projectRepository = projectRepository;
            _billingManager = billingManager;
        }

        #region CRUD

        /// <summary>
        /// Get by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="page">The page parameter is used for pagination. This value must be greater than 0.</param>
        /// <param name="limit">A limit on the number of objects to be returned. Limit can range between 1 and 100 items.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet("~/" + API_PREFIX + "/projects/{projectId:objectid}/webhooks")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(List<WebHook>))]
        public async Task<IActionResult> GetByProjectAsync(string projectId, [FromQuery] int page = 1, [FromQuery] int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var results = await _repository.GetByProjectIdAsync(projectId, o => o.PageNumber(page).PageLimit(limit));
            return OkWithResourceLinks(results.Documents, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the web hook.</param>
        /// <response code="404">The web hook could not be found.</response>
        [HttpGet("{id:objectid}", Name = "GetWebHookById")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(WebHook))]
        public Task<IActionResult> GetByIdAsync(string id) {
            return GetByIdImplAsync(id);
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="webhook">The web hook.</param>
        /// <returns></returns>
        /// <response code="400">An error occurred while creating the web hook.</response>
        /// <response code="409">The web hook already exists.</response>
        [HttpPost]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        public Task<IActionResult> PostAsync([FromBody] NewWebHook webhook) {
            return PostImplAsync(webhook);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of web hook identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more web hooks were not found.</response>
        /// <response code="500">An error occurred while deleting one or more web hooks.</response>
        [HttpDelete("{ids:objectids}")]
        [Authorize(Policy = AuthorizationRoles.UserPolicy)]
        [SwaggerResponse(StatusCodes.Status202Accepted, Type = typeof(IEnumerable<string>))]
        public Task<IActionResult> DeleteAsync(string ids) {
            return DeleteImplAsync(ids.FromDelimitedString());
        }

        #endregion

        /// <summary>
        /// This controller action is called by zapier to create a hook subscription.
        /// </summary>
        [HttpPost("subscribe")]
        [HttpPost("~/api/v{apiVersion:int=2}/webhooks/subscribe")]
        [HttpPost("~/api/v1/projecthook/subscribe")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public Task<IActionResult> SubscribeAsync([FromBody] JObject data, int apiVersion = 1) {
            var webHook = new NewWebHook {
                EventTypes = new[] { data.GetValue("event").Value<string>() },
                Url = data.GetValue("target_url").Value<string>(),
                Version = new Version(apiVersion >= 0 ? apiVersion : 0, 0)
            };

            if (!webHook.Url.StartsWith("https://hooks.zapier.com"))
                return Task.FromResult<IActionResult>(NotFound());

            string projectId = User.GetProjectId();
            if (projectId != null)
                webHook.ProjectId = projectId;
            else
                webHook.OrganizationId = Request.GetDefaultOrganizationId();

            return PostImplAsync(webHook);
        }

        /// <summary>
        /// This controller action is called by zapier to remove a hook subscription.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("unsubscribe")]
        [HttpPost("~/api/v1/projecthook/unsubscribe")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UnsubscribeAsync([FromBody] JObject data) {
            string targetUrl = data.GetValue("target_url").Value<string>();

            // don't let this anon method delete non-zapier hooks
            if (!targetUrl.StartsWith("https://hooks.zapier.com"))
                return NotFound();

            var results = await _repository.GetByUrlAsync(targetUrl);
            if (results.Documents.Count > 0) {
                string organizationId = results.Documents.First().OrganizationId;
                if (results.Documents.Any(h => h.OrganizationId != organizationId))
                    throw new ArgumentException("All OrganizationIds must be the same.");

                _logger.LogInformation("Removing {Count} zapier urls matching: {Url}", results.Documents.Count, targetUrl);
                await _repository.RemoveAsync(results.Documents);
            }

            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to test auth.
        /// </summary>
        [HttpGet("test")]
        [HttpPost("test")]
        [HttpGet("~/api/v1/projecthook/test")]
        [HttpPost("~/api/v1/projecthook/test")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Test() {
            return Ok(new[] {
                new { id = 1, Message = "Test message 1." },
                new { id = 2, Message = "Test message 2." }
            });
        }

        protected override async Task<WebHook> GetModelAsync(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            var webHook = await _repository.GetByIdAsync(id, o => o.Cache(useCache));
            if (webHook == null)
                return null;

            if (!String.IsNullOrEmpty(webHook.OrganizationId) && !IsInOrganization(webHook.OrganizationId))
                return null;

            if (!String.IsNullOrEmpty(webHook.ProjectId) && !await IsInProjectAsync(webHook.ProjectId))
                return null;

            return webHook;
        }

        protected override async Task<IReadOnlyCollection<WebHook>> GetModelsAsync(string[] ids, bool useCache = true) {
            if (ids == null || ids.Length == 0)
                return EmptyModels;

            var webHooks = await _repository.GetByIdsAsync(ids, o => o.Cache(useCache));
            if (webHooks.Count == 0)
                return EmptyModels;

            var results = new List<WebHook>();
            foreach (var webHook in webHooks) {
                if ((!String.IsNullOrEmpty(webHook.OrganizationId) && IsInOrganization(webHook.OrganizationId))
                    || (!String.IsNullOrEmpty(webHook.ProjectId) && (await IsInProjectAsync(webHook.ProjectId))))
                    results.Add(webHook);
            }

            return results;
        }

        protected override async Task<PermissionResult> CanAddAsync(WebHook value) {
            if (String.IsNullOrEmpty(value.Url) || value.EventTypes.Length == 0)
                return PermissionResult.Deny;

            if (String.IsNullOrEmpty(value.ProjectId) && String.IsNullOrEmpty(value.OrganizationId))
                return PermissionResult.Deny;

            if (!String.IsNullOrEmpty(value.OrganizationId) && !IsInOrganization(value.OrganizationId))
                return PermissionResult.DenyWithMessage("Invalid organization id specified.");

            Project project = null;
            if (!String.IsNullOrEmpty(value.ProjectId)) {
                project = await GetProjectAsync(value.ProjectId);
                if (project == null)
                    return PermissionResult.DenyWithMessage("Invalid project id specified.");

                value.OrganizationId = project.OrganizationId;
            }

            if (!await _billingManager.HasPremiumFeaturesAsync(project != null ? project.OrganizationId : value.OrganizationId))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add integrations.");

            return PermissionResult.Allow;
        }

        protected override Task<WebHook> AddModelAsync(WebHook value) {
            int version = IsValidWebHookVersion(value.Version) ? value.Version.Major : 2;
            value.Version = new Version(version, 0, 0, 0);

            return base.AddModelAsync(value);
        }

        protected override async Task<PermissionResult> CanDeleteAsync(WebHook value) {
            if (!String.IsNullOrEmpty(value.ProjectId) && !await IsInProjectAsync(value.ProjectId))
                return PermissionResult.DenyWithNotFound(value.Id);

            if (!String.IsNullOrEmpty(value.OrganizationId) && !IsInOrganization(value.OrganizationId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return PermissionResult.Allow;
        }

        private async Task<Project> GetProjectAsync(string projectId, bool useCache = true) {
            if (String.IsNullOrEmpty(projectId))
                return null;

            var project = await _projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return null;

            return project;
        }

        private async Task<bool> IsInProjectAsync(string projectId) {
            var project = await GetProjectAsync(projectId);
            return project != null;
        }

        private bool IsValidWebHookVersion(Version version) {
            return version != null && version.Major >= 1 && version.Major <= 2;
        }
    }
}
