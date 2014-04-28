using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "project")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class ProjectController : ApiController {
        private const string API_PREFIX = "api/v1/";
        private List<Project> _projects;
        private readonly IProjectRepository _projectRepository;

        public ProjectController(IProjectRepository projectRepository) {
            _projectRepository = projectRepository;
        }

        [HttpGet]
        [Route("config")]
        [Route("config/{projectId}")]
        [OverrideAuthorization]
        [Authorize(Roles = AuthorizationRoles.UserOrClient)]
        public IHttpActionResult Config(string projectId = null) {
            // TODO: Only the client should be using this..

            if (String.IsNullOrEmpty(projectId))
                projectId = User.GetApiKeyProjectId();
            
            if (String.IsNullOrEmpty(projectId))
                    return NotFound();

            var project = _projectRepository.GetByIdCached(projectId);
            if (project == null || !Request.CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return Ok(project.Configuration);
        }

        [HttpGet]
        [Route("is-name-available")]
        public IHttpActionResult IsNameAvailable(string id, string name) {
            if (String.IsNullOrEmpty(id) || String.IsNullOrWhiteSpace(name))
                return Ok(false);

            foreach (Project project in Projects) {
                if (String.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)) {
                    if (String.Equals(project.Id, id, StringComparison.OrdinalIgnoreCase))
                        break;

                    return Ok(false);
                }
            }

            return Ok(true);
        }

        private IEnumerable<Project> Projects {
            get {
                if (User == null)
                    return new List<Project>();

                if (_projects == null)
                    _projects = _projectRepository.GetByOrganizationIds(Request.GetAssociatedOrganizationIds()).ToList();

                return _projects;
            }
        }
    }
}