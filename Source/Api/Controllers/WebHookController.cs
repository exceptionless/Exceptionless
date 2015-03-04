using System;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Api.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Admin;
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
        
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/webhooks")]
        public IHttpActionResult GetByProject(string projectId, int page = 1, int limit = 10) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = _projectRepository.GetById(projectId);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            page = GetPage(page);
            limit = GetLimit(limit);
            var options = new PagingOptions { Page = page, Limit = limit };
            var results = _repository.GetByProjectId(projectId, options);
            return OkWithResourceLinks(results, options.HasMore && !NextPageExceedsSkipLimit(page, limit), page);
        }

        [HttpGet]
        [Route("{id:objectid}", Name = "GetWebHookById")]
        public override IHttpActionResult GetById(string id) {
            return base.GetById(id);
        }

        [Route]
        [HttpPost]
        public override IHttpActionResult Post(NewWebHook value) {
            return base.Post(value);
        }

        [HttpDelete]
        [Route("{ids:objectids}")]
        public override Task<IHttpActionResult> Delete([CommaDelimitedArray]string[] ids) {
            return base.Delete(ids);
        }

        #endregion

        /// <summary>
        /// This controller action is called by zapier to create a hook subscription.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("subscribe")]
        [Route("~/api/v{version:int=2}/webhooks/subscribe")]
        [Route("~/api/v1/projecthook/subscribe")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.Client)]
        public IHttpActionResult Subscribe(JObject data, int version = 1) {
            var webHook = new NewWebHook {
                EventTypes = new[] { data.GetValue("event").Value<string>() },
                Url = data.GetValue("target_url").Value<string>(),
                Version = new Version(version >= 0 ? version : 0, 0)
            };

            if (User.GetProjectId() != null)
                webHook.ProjectId = User.GetProjectId();
            else
                webHook.OrganizationId = Request.GetDefaultOrganizationId();

            return Post(webHook);
        }

        /// <summary>
        /// This controller action is called by zapier to remove a hook subscription.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [Route("unsubscribe")]
        [Route("~/api/v1/projecthook/unsubscribe")]
        public IHttpActionResult Unsubscribe(JObject data) {
            var targetUrl = data.GetValue("target_url").Value<string>();

            // don't let this anon method delete non-zapier hooks
            if (!targetUrl.Contains("zapier"))
                return NotFound();

            _repository.RemoveByUrl(targetUrl);
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
        public IHttpActionResult Test() {
            return Ok(new[] {
                new { id = 1, Message = "Test message 1." },
                new { id = 2, Message = "Test message 2." }
            });
        }

        protected override WebHook GetModel(string id, bool useCache = true) {
            var model = base.GetModel(id, useCache);
            return model != null && IsInProject(model.ProjectId) ? model : null;
        }

        protected override PermissionResult CanAdd(WebHook value) {
            if (String.IsNullOrEmpty(value.Url) || value.EventTypes.Length == 0)
                return PermissionResult.Deny;

            if (String.IsNullOrEmpty(value.ProjectId) && String.IsNullOrEmpty(value.OrganizationId))
                return PermissionResult.Deny;

            Project project = null;
            if (!String.IsNullOrEmpty(value.ProjectId)) {
                project = _projectRepository.GetById(value.ProjectId, true);
                if (!IsInProject(project))
                    return PermissionResult.Deny;

                if (!String.IsNullOrEmpty(value.OrganizationId))
                    value.OrganizationId = project.OrganizationId;
            } else if (!IsInOrganization(value.OrganizationId)) {
                return PermissionResult.Deny;
            }

            if (!_billingManager.HasPremiumFeatures(project != null ? project.OrganizationId : value.OrganizationId))
                return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add integrations.");

            return base.CanAdd(value);
        }

        protected override WebHook AddModel(WebHook value) {
            if (!IsValidWebHookVersion(value.Version))
                value.Version = new Version(2, 0);

            return base.AddModel(value);
        }

        protected override PermissionResult CanDelete(WebHook value) {
            if (!IsInProject(value.ProjectId))
                return PermissionResult.DenyWithNotFound(value.Id);

            return base.CanDelete(value);
        }

        private bool IsInProject(string projectId) {
            if (String.IsNullOrEmpty(projectId))
                return false;

            return IsInProject(_projectRepository.GetById(projectId, true));
        }

        private bool IsInProject(Project value) {
            if (value == null)
                return false;

            return IsInOrganization(value.OrganizationId);
        }

        private bool IsValidWebHookVersion(Version version) {
            return version != null && version.Major >= 1 && version.Major <= 2;
        }
    }
}