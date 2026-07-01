using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Foundatio.Mediator;

namespace Exceptionless.Web.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/admin")
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription();

        group.MapGet("echo", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminEcho(httpContext))).ToHttpResult(resultMapper));

        group.MapPost("change-plan", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string organizationId, string planId)
            => (await mediator.InvokeAsync<Result<object>>(new AdminChangePlan(organizationId, planId, httpContext))).ToHttpResult(resultMapper));

        group.MapPost("set-bonus", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string organizationId, int bonusEvents, DateTime? expires = null)
            => (await mediator.InvokeAsync<Result>(new AdminSetBonus(organizationId, bonusEvents, expires, httpContext))).ToHttpResult(resultMapper));

        return endpoints;
    }
}
