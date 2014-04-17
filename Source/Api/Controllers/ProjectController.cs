using System;
using System.Net.Http;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "project")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : ApiController {
        private const string API_PREFIX = "api/v1/";
        private readonly IProjectRepository _repository;

        public ProjectController(IProjectRepository repository) {
            _repository = repository;
        }

        [HttpGet]
        [Route("config")]
        [Route("config/{projectId}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string projectId = null) {
            // TODO: Only the client should be using this..

            if (String.IsNullOrEmpty(projectId)) {
                var ctx = Request.GetOwinContext();
                if (ctx == null || ctx.Request == null || ctx.Request.User == null)
                    return NotFound();

                projectId = ctx.Request.User.GetApiKeyProjectId();
                if (String.IsNullOrEmpty(projectId))
                    return NotFound();
            }

            var project = _repository.GetByIdCached(projectId);
            if (project == null) // || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }
    }
}