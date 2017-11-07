using System;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stripe;
#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    [Route(API_PREFIX + "/stripe")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class StripeController : ExceptionlessApiController {
        private readonly StripeEventHandler _stripeEventHandler;
        private readonly ILogger _logger;

        public StripeController(StripeEventHandler stripeEventHandler, ILogger<StripeController> logger) {
            _stripeEventHandler = stripeEventHandler;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] string json) {
            using (_logger.BeginScope(new ExceptionlessState().SetHttpContext(HttpContext))) {
                StripeEvent stripeEvent;
                try {
                    stripeEvent = StripeEventUtility.ParseEvent(json);
                } catch (Exception ex) {
                    using (_logger.BeginScope(new ExceptionlessState().Property("event", json)))
                        _logger.LogError(ex, "Unable to parse incoming event.");

                    return BadRequest("Unable to parse incoming event");
                }

                if (stripeEvent == null) {
                    _logger.LogWarning("Null stripe event.");
                    return BadRequest("Incoming event empty");
                }

                await _stripeEventHandler.HandleEventAsync(stripeEvent);

                return Ok();
            }
        }
    }
}