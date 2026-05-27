using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using IMediator = Foundatio.Mediator.IMediator;

namespace Exceptionless.Web.Api.Endpoints;

public static class StripeEndpoints
{
    public static IEndpointRouteBuilder MapStripeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("api/v2/stripe", async (HttpContext httpContext, IMediator mediator) =>
        {
            using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
            string json = await reader.ReadToEndAsync();
            string? signature = httpContext.Request.Headers["Stripe-Signature"];
            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new HandleStripeWebhook(json, signature));
        })
        .AddEndpointFilter<AutoValidationEndpointFilter>()
        .AllowAnonymous()
        .ExcludeFromDescription();

        return endpoints;
    }
}
