using System;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Models;

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
        //[Route("config/{projectId}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string projectId = null) {
            // TODO: Only the client should be using this..
           // if (User.Identity.AuthenticationType.Equals("ApiKey"))
           //     return Ok(User.Project.Configuration);

            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _repository.GetById(projectId);
            if (project == null) // || !User.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }
    }
}