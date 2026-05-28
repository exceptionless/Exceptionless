using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Foundatio.Mediator;
using Stripe;

namespace Exceptionless.Web.Api.Handlers;

public class StripeHandler(
    StripeEventHandler stripeEventHandler,
    StripeOptions stripeOptions,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StripeHandler>();
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result> Handle(HandleStripeWebhook message)
    {
        using (_logger.BeginScope(new ExceptionlessState().SetHttpContext(HttpContext).Property("event", message.Json)))
        {
            if (String.IsNullOrEmpty(message.Json))
            {
                _logger.LogWarning("Unable to get json of incoming event");
                return Result.BadRequest("Unable to get json of incoming event.");
            }

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(message.Json, message.Signature ?? String.Empty, stripeOptions.StripeWebHookSigningSecret, throwOnApiVersionMismatch: false);
            }
            catch (Exception ex) when (ex is StripeException or System.Text.Json.JsonException or ArgumentException)
            {
                _logger.LogError(ex, "Unable to parse incoming event with {Signature}: {Message}", message.Signature, ex.Message);
                return Result.BadRequest("Unable to parse incoming event.");
            }

            if (stripeEvent is null)
            {
                _logger.LogWarning("Null stripe event");
                return Result.BadRequest("Null stripe event.");
            }

            await stripeEventHandler.HandleEventAsync(stripeEvent);
            return Result.Success();
        }
    }
}
