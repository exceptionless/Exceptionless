using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Messages;
using IMediator = Foundatio.Mediator.IMediator;

namespace Exceptionless.Web.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/admin")
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .ExcludeFromDescription();

        group.MapGet("settings", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminSettings()));

        group.MapGet("stats", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminStats()));

        group.MapGet("migrations", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminMigrations()));

        group.MapGet("echo", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminEcho(httpContext)));

        group.MapGet("assemblies", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminAssemblies()));

        group.MapPost("change-plan", async (HttpContext httpContext, IMediator mediator, string organizationId, string planId)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AdminChangePlan(organizationId, planId, httpContext)));

        group.MapPost("set-bonus", async (HttpContext httpContext, IMediator mediator, string organizationId, int bonusEvents, DateTime? expires = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AdminSetBonus(organizationId, bonusEvents, expires, httpContext)));

        group.MapGet("requeue", async (IMediator mediator, string? path = null, bool archive = false)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AdminRequeue(path, archive)));

        group.MapGet("maintenance/{name}", async (string name, IMediator mediator, DateTime? utcStart = null, DateTime? utcEnd = null, string? organizationId = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AdminRunMaintenance(name, utcStart, utcEnd, organizationId)));

        group.MapGet("elasticsearch", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminElasticsearch()));

        group.MapGet("elasticsearch/snapshots", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAdminElasticsearchSnapshots()));

        group.MapPost("generate-sample-events", async (IMediator mediator, int eventCount = 250, int daysBack = 7)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AdminGenerateSampleEvents(eventCount, daysBack)));

        return endpoints;
    }
}
