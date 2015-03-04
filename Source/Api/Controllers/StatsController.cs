using System;
using System.Web.Http;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Stats;
using NLog.Fluent;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stats")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StatsController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly EventStats _stats;

        public StatsController(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository, EventStats stats) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _stats = stats;
        }

        [HttpGet]
        [Route]
        public IHttpActionResult Get(string filter = null, string time = null, string offset = null) {
            return GetInternal(null, filter, time, offset);
        }

        public IHttpActionResult GetInternal(string systemFilter, string userFilter = null, string time = null, string offset = null) {
            var timeInfo = GetTimeInfo(time, offset);

            var processResult = QueryProcessor.Process(userFilter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = GetAssociatedOrganizationsFilter(_organizationRepository, processResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter));

            EventStatsResult result;
            try {
                result = _stats.GetOccurrenceStats(timeInfo.UtcRange.Start, timeInfo.UtcRange.End, systemFilter, processResult.ExpandedQuery, timeInfo.Offset);
            } catch (ApplicationException ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().SetProperty("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Time = time, Offset = offset }).AddTags("Search").Submit();
                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stats")]
        public IHttpActionResult GetByProject(string projectId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            Project project = _projectRepository.GetById(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("project:", projectId), filter, time, offset);
        }

        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/stats")]
        public IHttpActionResult GetByStack(string stackId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            Stack stack = _stackRepository.GetById(stackId);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            return GetInternal(String.Concat("stack:", stackId), filter, time, offset);
        }
    }
}