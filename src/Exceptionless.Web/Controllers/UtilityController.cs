using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Queries.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers {
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route(API_PREFIX)]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class UtilityController : ExceptionlessApiController {
        private readonly PersistentEventQueryValidator _eventQueryValidator;
        private readonly StackQueryValidator _stackQueryValidator;

        public UtilityController(PersistentEventQueryValidator eventQueryValidator, StackQueryValidator stackQueryValidator) {
            _eventQueryValidator = eventQueryValidator;
            _stackQueryValidator = stackQueryValidator;
        }

        /// <summary>
        /// Validate search query
        /// </summary>
        /// <remarks>
        /// Validate a search query to ensure that it can successfully be searched by the api
        /// </remarks>
        /// <param name="query">The query you wish to validate.</param>
        [HttpGet("search/validate")]
        public async Task<ActionResult<QueryValidator.QueryProcessResult>> ValidateAsync(string query) {
            var eventResults = await _eventQueryValidator.ValidateQueryAsync(query);
            var stackResults = await _stackQueryValidator.ValidateQueryAsync(query);
            return Ok(new QueryValidator.QueryProcessResult {
                IsValid = eventResults.IsValid || stackResults.IsValid,
                UsesPremiumFeatures = eventResults.UsesPremiumFeatures && stackResults.UsesPremiumFeatures,
                Message = eventResults.Message ?? stackResults.Message
            });
        }
    }
}