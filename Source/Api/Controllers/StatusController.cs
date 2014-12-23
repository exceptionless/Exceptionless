using System;
using System.Net;
using System.Web.Http;
using Exceptionless.Core.Utility;

namespace Exceptionless.Api.Controllers {
    public class StatusController : ExceptionlessApiController {
        private readonly SystemHealthChecker _healthChecker;

        public StatusController(SystemHealthChecker healthChecker) {
            _healthChecker = healthChecker;
        }

        [HttpGet]
        [Route(API_PREFIX + "/status")]
        public IHttpActionResult Index() {
            var result = _healthChecker.CheckAll();
            if (!result.IsHealthy)
                return StatusCodeWithMessage(HttpStatusCode.ServiceUnavailable, result.Message);

            return Ok(new { Message = "All Systems Check" });
        }
    }
}
