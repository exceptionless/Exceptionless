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
        [Authorize(Roles = AuthorizationRoles.User)]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(QueryValidator.QueryProcessResult))]
        public async Task<IActionResult> ValidateAsync([FromQuery] string query) {
            return Ok(await _validator.ValidateQueryAsync(query));
        }

        [HttpGet("notfound")]
        [HttpPut("notfound")]
        [HttpPatch("notfound")]
        [HttpPost("notfound")]
        [HttpHead("notfound")]
        public IActionResult Http404([FromQuery] string link) {
            return Ok(new {
                Message = "Not found",
                Url = "http://docs.exceptionless.io"
            });
        }

        [HttpGet("boom")]
        [HttpPut("boom")]
        [HttpPatch("boom")]
        [HttpPost("boom")]
        [HttpHead("boom")]
        public IActionResult Boom() {
            throw new ApplicationException("Boom!");
        }
    }
}
