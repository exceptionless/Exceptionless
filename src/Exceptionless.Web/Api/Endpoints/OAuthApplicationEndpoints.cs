using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models.Admin;
using Foundatio.Mediator;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Api.Endpoints;

public static class OAuthApplicationEndpoints
{
    public static IEndpointRouteBuilder MapOAuthApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/admin/oauth-applications")
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription();

        endpoints.MapGet("api/v2/admin/oauth-applications", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<ViewOAuthApplication>>>(new GetOAuthApplications())).ToHttpResult(resultMapper))
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription();

        endpoints.MapPost("api/v2/admin/oauth-applications", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] NewOAuthApplication model)
            => (await mediator.InvokeAsync<Result<ViewOAuthApplication>>(new CreateOAuthApplicationMessage(model, httpContext))).ToHttpResult(resultMapper))
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription()
            .Produces<ViewOAuthApplication>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] UpdateOAuthApplication model)
            => (await mediator.InvokeAsync<Result<ViewOAuthApplication>>(new UpdateOAuthApplicationMessage(id, model, httpContext))).ToHttpResult(resultMapper))
            .Produces<ViewOAuthApplication>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new DeleteOAuthApplicationMessage(id))).ToHttpResult(resultMapper))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
