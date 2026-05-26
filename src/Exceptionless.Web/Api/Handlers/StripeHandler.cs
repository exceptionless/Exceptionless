using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Stripe;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Handlers;

public class StripeHandler(
    StripeEventHandler stripeEventHandler,
    StripeOptions stripeOptions,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StripeHandler>();
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<IResult> Handle(HandleStripeWebhook message)
    {
        using (_logger.BeginScope(new ExceptionlessState().SetHttpContext(HttpContext).Property("event", message.Json)))
        {
            if (String.IsNullOrEmpty(message.Json))
            {
                _logger.LogWarning("Unable to get json of incoming event");
                return HttpResults.BadRequest();
            }

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(message.Json, message.Signature ?? String.Empty, stripeOptions.StripeWebHookSigningSecret, throwOnApiVersionMismatch: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to parse incoming event with {Signature}: {Message}", message.Signature, ex.Message);
                return HttpResults.BadRequest();
            }

            if (stripeEvent is null)
            {
                _logger.LogWarning("Null stripe event");
                return HttpResults.BadRequest();
            }

            await stripeEventHandler.HandleEventAsync(stripeEvent);
            return HttpResults.Ok();
        }
    }
}
