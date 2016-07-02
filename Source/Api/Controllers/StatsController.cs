using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
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
        private readonly ILogger _logger;

        public StatsController(IOrganizationRepository organizationRepository, EventStats stats, ILogger<StatsController> logger) {
            _organizationRepository = organizationRepository;
            _stats = stats;
            _logger = logger;
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
            
            var pr = QueryProcessor.Process(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            var organizations = await GetAssociatedOrganizationsAsync(_organizationRepository);
            var sf = BuildSystemFilter(organizations, filter, far.UsesPremiumFeatures || pr.UsesPremiumFeatures);
            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());

            NumbersStatsResult result;
            try {
                result = await _stats.GetNumbersStatsAsync(far.Aggregations, ti.UtcRange.Start, ti.UtcRange.End, sf, pr.ExpandedQuery, ti.Offset);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex)
                    .Message("An error has occurred. Please check your search filter.")
                    .Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = time, Offset = offset })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext).Write();

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
            
            var pr = QueryProcessor.Process(filter);
            if (!pr.IsValid)
                return BadRequest(pr.Message);

            var organizations = await GetAssociatedOrganizationsAsync(_organizationRepository);
            var sf = BuildSystemFilter(organizations, filter, far.UsesPremiumFeatures || pr.UsesPremiumFeatures);
            var ti = GetTimeInfo(time, offset, organizations.GetRetentionUtcCutoff());

            NumbersTimelineStatsResult result;
            try {
                result = await _stats.GetNumbersTimelineStatsAsync(far.Aggregations, ti.UtcRange.Start, ti.UtcRange.End, sf, pr.ExpandedQuery, ti.Offset);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex)
                    .Message("An error has occurred. Please check your search filter.")
                    .Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = time, Offset = offset })
                    .Tag("Search")
                    .Identity(ExceptionlessUser.EmailAddress)
                    .Property("User", ExceptionlessUser)
                    .SetActionContext(ActionContext).Write();

                return BadRequest("An error has occurred. Please check your search filter.");
            }

            return Ok(result);
        }
    }
}
