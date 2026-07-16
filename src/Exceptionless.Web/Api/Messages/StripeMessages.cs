namespace Exceptionless.Web.Api.Messages;

public record HandleStripeWebhook(string Json, string? Signature);
