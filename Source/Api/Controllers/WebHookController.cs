using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Repositories.Models;
using Newtonsoft.Json.Linq;

namespace Exceptionless.App.Controllers.API {
    [RoutePrefix(API_PREFIX + "/webhooks")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class WebHookController : RepositoryApiController<IWebHookRepository, WebHook, WebHook, NewWebHook, NewWebHook> {
        private readonly IProjectRepository _projectRepository;
        private readonly BillingManager _billingManager;

        public WebHookController(IWebHookRepository repository, IProjectRepository projectRepository, BillingManager billingManager) : base(repository) {
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
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/webhooks")]
        [ResponseType(typeof(List<WebHook>))]
        public async Task<IHttpActionResult> GetByProjectAsync(string projectId, int page = 1, int limit = 10) {
            var project = await GetProjectAsync(projectId);
            if (project == null)
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = await _repository.GetByProjectIdAsync(projectId, options, true);
            return OkWithResourceLinks(results.Documents, results.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        /// <summary>
        /// Get by id
        /// </summary>
        /// <param name="id">The identifier of the web hook.</param>
        /// <response code="404">The web hook could not be found.</response>
        [HttpGet]
        [Route("{id:objectid}", Name = "GetWebHookById")]
        [ResponseType(typeof(WebHook))]
        public override Task<IHttpActionResult> GetByIdAsync(string id) {
            return base.GetByIdAsync(id);
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="webhook">The web hook.</param>
        /// <returns></returns>
        /// <response code="400">An error occurred while creating the web hook.</response>
        /// <response code="409">The web hook already exists.</response>
        [Route]
        [HttpPost]
        public override Task<IHttpActionResult> PostAsync(NewWebHook webhook) {
            return base.PostAsync(webhook);
        }

        /// <summary>
        /// Remove
        /// </summary>
        /// <param name="ids">A comma delimited list of web hook identifiers.</param>
        /// <response code="204">No Content.</response>
        /// <response code="400">One or more validation errors occurred.</response>
        /// <response code="404">One or more web hooks were not found.</response>
        /// <response code="500">An error occurred while deleting one or more web hooks.</response>
        [HttpDelete]
        [Route("{ids:objectids}")]
        public Task<IHttpActionResult> DeleteAsync(string ids) {
            return base.DeleteAsync(ids.FromDelimitedString());
        }

        #endregion

        /// <summary>
        /// This controller action is called by zapier to create a hook subscription.
        /// </summary>
        [HttpPost]
        [Route("subscribe")]
        [Route("~/api/v{version:int=2}/webhooks/subscribe")]
        [Route("~/api/v1/projecthook/subscribe")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public Task<IHttpActionResult> SubscribeAsync(JObject data, int version = 1) {
            var webHook = new NewWebHook {
                EventTypes = new[] { data.GetValue("event").Value<string>() },
                Url = data.GetValue("target_url").Value<string>(),
                Version = new Version(version >= 0 ? version : 0, 0)
            };

            if (User.GetProjectId() != null)
                webHook.ProjectId = User.GetProjectId();
            else
                webHook.OrganizationId = Request.GetDefaultOrganizationId();

            return PostAsync(webHook);
        }

        /// <summary>
        /// This controller action is called by zapier to remove a hook subscription.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [Route("unsubscribe")]
        [Route("~/api/v1/projecthook/unsubscribe")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IHttpActionResult> UnsubscribeAsync(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();

            // don't let this anon method delete non-zapier hooks
            if (!targetUrl.Contains("zapier"))
                return NotFound();

            await _repository.RemoveByUrlAsync(targetUrl);
            return Ok();
        }

        /// <summary>
        /// This controller action is called by zapier to test auth.
        /// </summary>
        [HttpGet]
        [HttpPost]
        [Route("test")]
        [Route("~/api/v1/projecthook/test")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IHttpActionResult Test() {
            return Ok(new[] {
                new { id = 1, Message = "Test message 1." },
                new { id = 2, Message = "Test message 2." }
            });
        }

        protected override async Task<WebHook> GetModelAsync(string id, bool useCache = true) {
            if (String.IsNullOrEmpty(id))
                return null;

            var webHook = await _repository.GetByIdAsync(id, useCache);
            if (webHook == null)
                return null;

            if (!String.IsNullOrEmpty(webHook.OrganizationId) && !IsInOrganization(webHook.OrganizationId))
                return null;

            if (!String.IsNullOrEmpty(webHook.ProjectId) && !await IsInProjectAsync(webHook.ProjectId))
                return null;

            return webHook;
        }

        protected override async Task<ICollection<WebHook>> GetModelsAsync(string[] ids, bool useCache = true) {
            var results = new List<WebHook>();
            if (ids == null || ids.Length == 0)
                return results;

            var webHooks = (await _repository.GetByIdsAsync(ids, useCache: useCache)).Documents;
            if (webHooks == null)
                return results;

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

            var project = await _projectRepository.GetByIdAsync(projectId, useCache);
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
