using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Foundatio.Mediator;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Endpoints;

public static class StripeEndpoints
{
    public static IEndpointRouteBuilder MapStripeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("api/v2/stripe", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper) =>
        {
            string? contentType = httpContext.Request.ContentType?.Split(';', 2)[0].Trim();
            if (!String.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
                return Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

            using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
            string json = await reader.ReadToEndAsync();
            string? signature = httpContext.Request.Headers["Stripe-Signature"];
            return (await mediator.InvokeAsync<Result>(new HandleStripeWebhook(json, signature))).ToHttpResult(resultMapper);
        })
        .AddEndpointFilter<AutoValidationEndpointFilter>()
        .AllowAnonymous()
        .ExcludeFromDescription();

        return endpoints;
    }
}
