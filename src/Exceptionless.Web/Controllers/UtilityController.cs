using System;
using System.Threading.Tasks;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Queries.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Controllers {
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route(API_PREFIX)]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
    public class UtilityController : ExceptionlessApiController {
        private readonly PersistentEventQueryValidator _validator;

        public UtilityController(PersistentEventQueryValidator validator) {
            _validator = validator;
        }

        /// <summary>
        /// Validate search query
        /// </summary>
        /// <remarks>
        /// Validate a search query to ensure that it can successfully be searched by the api
        /// </remarks>
        /// <param name="query">The query you wish to validate.</param>
        [HttpGet("search/validate")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(QueryValidator.QueryProcessResult))]
        public async Task<IActionResult> ValidateAsync([FromQuery] string query) {
            return Ok(await _validator.ValidateQueryAsync(query));
        }
    }
}