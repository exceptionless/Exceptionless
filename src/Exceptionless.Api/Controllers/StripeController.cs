using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Billing;
using Exceptionless.Api.Utility;
using Microsoft.Extensions.Logging;
using Stripe;
#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stripe")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class StripeController : ExceptionlessApiController {
        private readonly StripeEventHandler _stripeEventHandler;
        private readonly ILogger _logger;

        public StripeController(StripeEventHandler stripeEventHandler, ILogger<StripeController> logger) {
            _stripeEventHandler = stripeEventHandler;
            _logger = logger;
        }

        [Route]
        [HttpPost]
        public async Task<IHttpActionResult> PostAsync([NakedBody]string json) {
            using (_logger.BeginScope(new ExceptionlessState().SetActionContext(ActionContext))) {
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