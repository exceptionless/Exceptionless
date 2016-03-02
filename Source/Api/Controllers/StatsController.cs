using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models.Stats;
using Foundatio.Logging;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stats")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class StatsController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly EventStats _stats;

        public StatsController(IOrganizationRepository organizationRepository, EventStats stats) {
            _organizationRepository = organizationRepository;
            _stats = stats;
        }
        
        /// <summary>
        /// Gets a list of numbers based on the passed in fields.
        /// </summary>
        /// <param name="fields">A comma delimited list of values you want returned. Example: avg:value,distinct:value,sum:users,max:value,min:value,last:value</param>
        /// <param name="filter">A filter that controls what data is returned from the server.</param>
        /// <param name="time">The time filter that limits the data being returned to a specific date range.</param>
        /// <param name="offset">The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.</param>
        [HttpGet]
        [Route]
        [ResponseType(typeof(NumbersStatsResult))]
        public async Task<IHttpActionResult> GetAsync(string fields = null, string filter = null, string time = null, string offset = null) {
            var far = FieldAggregationProcessor.Process(fields);
            if (!far.IsValid)
                return BadRequest(far.Message);
            
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
        public async Task<IHttpActionResult> GetTimelineAsync(string fields = null, string filter = null, string time = null, string offset = null) {
            var far = FieldAggregationProcessor.Process(fields);
            if (!far.IsValid)
                return BadRequest(far.Message);
            
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
