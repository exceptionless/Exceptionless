﻿using System;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Core.Billing;
using Exceptionless.Api.Utility;
using NLog.Fluent;
using Stripe;
#pragma warning disable 1998

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/stripe")]
    [ApiExplorerSettings(IgnoreApi = true)]
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
                Log.Error().Exception(ex).Message("Unable to parse incoming event.").Property("event", json).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Unable to parse incoming event");
            }

            if (stripeEvent == null) {
                Log.Warn().Message("Null stripe event.").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Incoming event empty");
            }

            _stripeEventHandler.HandleEvent(stripeEvent);

            return Ok();
        }
    }
}