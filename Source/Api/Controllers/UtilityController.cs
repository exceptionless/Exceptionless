using System;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Filter;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX)]
    public class UtilityController : ExceptionlessApiController {
        /// <summary>
        /// Validate search query
        /// </summary>
        /// <remarks>
        /// Validate a search query to ensure that it can successfully be searched by the api
        /// </remarks>
        /// <param name="query">The query you wish to validate.</param>
        [HttpGet]
        [Route("search/validate")]
        [Authorize(Roles = AuthorizationRoles.User)]
        [ResponseType(typeof(QueryProcessResult))]
        public IHttpActionResult Validate(string query) {
            return Ok(QueryProcessor.Process(query));
        }

        [Route("notfound")]
        [HttpGet, HttpPut, HttpPatch, HttpPost, HttpHead]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IHttpActionResult Http404(string link) {
            return Ok(new {
                Message = "Not found",
                Url = "http://docs.exceptionless.io"
            });
        }

        [Route("boom")]
        [HttpGet, HttpPut, HttpPatch, HttpPost, HttpHead]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IHttpActionResult Boom() {
            throw new ApplicationException("Boom!");
        }
    }
}
