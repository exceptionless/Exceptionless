using System;
using System.Web.Http;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX)]
    public class UtilityController : ExceptionlessApiController {
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
