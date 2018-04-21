using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Stripe;
#pragma warning disable 1998

namespace Exceptionless.Web.Controllers {
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
        public async Task<IActionResult> PostAsync(JObject data) {
            using (_logger.BeginScope(new ExceptionlessState().SetHttpContext(HttpContext).Property("event", data))) {
                string json = data?.ToString();
                if (String.IsNullOrEmpty(json)) {
                    _logger.LogWarning("Unable to get json of incoming event.");
                    return BadRequest();
                }

                if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature) || String.IsNullOrEmpty(signature)) {
                    _logger.LogWarning("No Stripe-Signature header was sent with incoming event.");
                    return BadRequest();
                }

                StripeEvent stripeEvent;
                try {
                    stripeEvent = StripeEventUtility.ConstructEvent(json, signature, Settings.Current.StripeWebHookSigningSecret);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Unable to parse incoming event: {Message}", ex.Message);
                    return BadRequest();
                }

                if (stripeEvent == null) {
                    _logger.LogWarning("Null stripe event.");
                    return BadRequest();
                }

                await _stripeEventHandler.HandleEventAsync(stripeEvent);
                return Ok();
            }
        }
    }
}