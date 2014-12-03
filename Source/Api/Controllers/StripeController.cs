using System;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Core.Billing;
using Exceptionless.Api.Utility;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stripe")]
    public class StripeController : ExceptionlessApiController {
        private readonly StripeEventHandler _stripeEventHandler;

        public StripeController(StripeEventHandler stripeEventHandler) {
            _stripeEventHandler = stripeEventHandler;
        }

        [Route]
        [HttpPost]
        public async Task<IHttpActionResult> Post([NakedBody]string json) {
            StripeEvent stripeEvent;
            try {
                stripeEvent = StripeEventUtility.ParseEvent(json);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to parse incoming event.").Report(b => b.AddObject(json, "Event")).Write();
                return BadRequest("Unable to parse incoming event");
            }

            if (stripeEvent == null) {
                Log.Warn().Message("Null stripe event.").Write();
                return BadRequest("Incoming event empty");
            }

            _stripeEventHandler.HandleEvent(stripeEvent);

            return Ok();
        }
    }
}