using System;
using System.Web.Http;
using Exceptionless.Core.Authorization;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX)]
    public class UtilityController : ExceptionlessApiController {
        [HttpGet]
        [Route("search/validate")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult Validate(string query) {
            if (String.IsNullOrWhiteSpace(query))
                return Ok();

            // TODO: Validate this with a parser.
            if (query.StartsWith("{") || query.EndsWith(":") || query.EndsWith("}"))
                return BadRequest("Invalid character in search query.");

            return Ok();
        }

        [Route("notfound")]
        [HttpGet, HttpPut, HttpPatch, HttpPost, HttpHead]
        public IHttpActionResult Http404(string link) {
            return Ok(new {
                Message = "Not found",
                Url = "http://docs.exceptionless.com"
            });
        }

        [Route("boom")]
        [HttpGet, HttpPut, HttpPatch, HttpPost, HttpHead]
        public IHttpActionResult Boom() {
            throw new ApplicationException("Boom!");
        }
    }
}
