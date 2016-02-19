using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Stats;
using Foundatio.Logging;

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

        /// <summary>
        /// Get all
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        [HttpGet]
        [Route]
        [ResponseType(typeof(EventStatsResult))]
        public Task<IHttpActionResult> GetAsync(string filter = null, string time = null, string offset = null) {
            return GetInternalAsync(null, filter, time, offset);
        }

        private async Task<IHttpActionResult> GetInternalAsync(string systemFilter, string userFilter = null, string time = null, string offset = null, bool usesPremiumFeatures = false) {
            var timeInfo = GetTimeInfo(time, offset);

            var processResult = QueryProcessor.Process(userFilter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            if (String.IsNullOrEmpty(systemFilter))
                systemFilter = await GetAssociatedOrganizationsFilterAsync(_organizationRepository, processResult.UsesPremiumFeatures || usesPremiumFeatures, HasOrganizationOrProjectFilter(userFilter));

            EventStatsResult result;
            try {
                result = await _stats.GetOccurrenceStatsAsync(timeInfo.UtcRange.Start, timeInfo.UtcRange.End, systemFilter, processResult.ExpandedQuery, timeInfo.Offset);
            } catch (ApplicationException ex) {
                Logger.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = userFilter, Time = time, Offset = offset })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }

        /// <summary>
        /// Get by project
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/projects/{projectId:objectid}/stats")]
        [ResponseType(typeof(EventStatsResult))]
        public async Task<IHttpActionResult> GetByProjectAsync(string projectId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(projectId))
                return NotFound();

            var project = await _projectRepository.GetByIdAsync(projectId, true);
            if (project == null || !CanAccessOrganization(project.OrganizationId))
                return NotFound();

            return await GetInternalAsync(String.Concat("project:", projectId), filter, time, offset);
        }

        /// <summary>
        /// Get by stack
        /// </summary>
        /// <param name="stackId">The identifier of the stack.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="404">The stack could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/stacks/{stackId:objectid}/stats")]
        [ResponseType(typeof(EventStatsResult))]
        public async Task<IHttpActionResult> GetByStackAsync(string stackId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(stackId))
                return NotFound();

            Stack stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack == null || !CanAccessOrganization(stack.OrganizationId))
                return NotFound();

            return await GetInternalAsync(String.Concat("stack:", stackId), filter, time, offset);
        }

        /// <summary>
        /// Get by Session
        /// </summary>
        /// <param name="sessionId">The identifier of the project.</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        /// <response code="404">The project could not be found.</response>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/sessions/{sessionId:identifier}/stats")]
        [ResponseType(typeof(EventStatsResult))]
        public async Task<IHttpActionResult> GetBySessionAsync(string sessionId, string filter = null, string time = null, string offset = null) {
            if (String.IsNullOrEmpty(sessionId))
                return NotFound();
            
            return await GetInternalAsync(null, $"ref.session:{sessionId} {filter}".TrimEnd(), time, offset, true);
        }
        
        /// <summary>
        /// Get all session stats
        /// </summary>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        [HttpGet]
        [Route("~/" + API_PREFIX + "/sessions/stats")]
        [ResponseType(typeof(EventStatsResult))]
        public async Task<IHttpActionResult> GetSessionsAsync(string filter = null, string time = null, string offset = null) {
            var timeInfo = GetTimeInfo(time, offset);

            var processResult = QueryProcessor.Process(filter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            string systemFilter = await GetAssociatedOrganizationsFilterAsync(_organizationRepository, true, HasOrganizationOrProjectFilter(filter));

            SessionStatsResult result;
            try {
                result = await _stats.GetSessionStatsAsync(timeInfo.UtcRange.Start, timeInfo.UtcRange.End, systemFilter, processResult.ExpandedQuery, timeInfo.Offset);
            } catch (ApplicationException ex) {
                Logger.Error().Exception(ex)
                    .Property("Search Filter", new { SystemFilter = systemFilter, UserFilter = filter, Time = time, Offset = offset })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext)
                    .Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }
        
        /// <summary>
        /// Gets a list of numbers based on the passed in fields.
        /// </summary>
        /// <param name="fields">A comma delimited list of values you want returned. Example: avg:value,distinct:value,sum:users,max:value,min:value,last:value</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        [HttpGet]
        [Route("numbers")]
        [ResponseType(typeof(NumbersStatsResult))]
        public async Task<IHttpActionResult> GetNumbersAsync(string fields, string filter = null, string time = null, string offset = null) {
            var far = FieldAggregationProcessor.Process(fields);
            if (!far.IsValid)
                return BadRequest(far.Message);

            return await GetNumbersInternalAsync(far, filter, time, offset);
        }

        private async Task<IHttpActionResult> GetNumbersInternalAsync(FieldAggregationsResult far, string filter, string time, string offset) {
            var processResult = QueryProcessor.Process(filter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            string systemFilter = await GetAssociatedOrganizationsFilterAsync(_organizationRepository, far.UsesPremiumFeatures || processResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(filter));

            NumbersStatsResult result;
            try {
                var timeInfo = GetTimeInfo(time, offset);
                result = await _stats.GetNumbersStatsAsync(far.Aggregations, timeInfo.UtcRange.Start, timeInfo.UtcRange.End, systemFilter, processResult.ExpandedQuery, timeInfo.Offset);
            } catch (ApplicationException ex) {
                Logger.Error().Exception(ex).Property("Search Filter", new {
                    SystemFilter = systemFilter,
                    UserFilter = filter,
                    Time = time,
                    Offset = offset
                }).Tag("Search").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets a timeline of data with buckets that contain list of numbers based on the passed in fields.
        /// </summary>
        /// <param name="fields">A comma delimited list of values you want returned. Example: avg:value,distinct:value,sum:users,max:value,min:value,last:value</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        [HttpGet]
        [Route("timeline")]
        [ResponseType(typeof(NumbersTimelineStatsResult))]
        public async Task<IHttpActionResult> GetTimelineAsync(string fields, string filter = null, string time = null, string offset = null) {
            var far = FieldAggregationProcessor.Process(fields);
            if (!far.IsValid)
                return BadRequest(far.Message);

            return await GetTimelineInternalAsync(far, filter, time, offset);
        }

        private async Task<IHttpActionResult> GetTimelineInternalAsync(FieldAggregationsResult far, string filter, string time, string offset) {
            var processResult = QueryProcessor.Process(filter);
            if (!processResult.IsValid)
                return BadRequest(processResult.Message);

            string systemFilter = await GetAssociatedOrganizationsFilterAsync(_organizationRepository, far.UsesPremiumFeatures || processResult.UsesPremiumFeatures, HasOrganizationOrProjectFilter(filter));

            NumbersTimelineStatsResult result;
            try {
                var timeInfo = GetTimeInfo(time, offset);
                result = await _stats.GetNumbersTimelineStatsAsync(far.Aggregations, timeInfo.UtcRange.Start, timeInfo.UtcRange.End, systemFilter, processResult.ExpandedQuery, timeInfo.Offset);
            } catch (ApplicationException ex) {
                Logger.Error().Exception(ex).Property("Search Filter", new {
                    SystemFilter = systemFilter,
                    UserFilter = filter,
                    Time = time,
                    Offset = offset
                }).Tag("Search").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }
    }
}
