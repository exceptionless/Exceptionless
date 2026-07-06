using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Api.Endpoints;

public static class ContactEndpoints
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("api/v2/contact", async (HttpContext httpContext, IMediator mediator, [FromBody] ContactRequest request)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitContactRequest(request, httpContext)))
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .AllowAnonymous()
            .Accepts<ContactRequest>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ExcludeFromDescription();

        endpoints.MapPost("api/v2/contact", async (HttpContext httpContext, IMediator mediator, [FromForm] ContactRequest request)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitContactRequest(request, httpContext)))
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .AllowAnonymous()
            .DisableAntiforgery()
            .Accepts<ContactRequest>("application/x-www-form-urlencoded", "multipart/form-data")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ExcludeFromDescription();

        return endpoints;
    }
}
