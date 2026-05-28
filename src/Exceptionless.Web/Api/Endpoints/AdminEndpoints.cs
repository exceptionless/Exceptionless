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

        group.MapGet("settings", async (IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminSettings())).ToHttpResult());

        group.MapGet("stats", async (IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminStats())).ToHttpResult());

        group.MapGet("migrations", async (IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminMigrations())).ToHttpResult());

        group.MapGet("echo", async (HttpContext httpContext, IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminEcho(httpContext))).ToHttpResult());

        group.MapGet("assemblies", async (IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminAssemblies())).ToHttpResult());

        group.MapPost("change-plan", async (HttpContext httpContext, IMediator mediator, string organizationId, string planId)
            => (await mediator.InvokeAsync<Result<object>>(new AdminChangePlan(organizationId, planId, httpContext))).ToHttpResult());

        group.MapPost("set-bonus", async (HttpContext httpContext, IMediator mediator, string organizationId, int bonusEvents, DateTime? expires = null)
            => (await mediator.InvokeAsync<Result>(new AdminSetBonus(organizationId, bonusEvents, expires, httpContext))).ToHttpResult());

        group.MapGet("requeue", async (IMediator mediator, string? path = null, bool archive = false)
            => (await mediator.InvokeAsync<Result<object>>(new AdminRequeue(path, archive))).ToHttpResult());

        group.MapGet("maintenance/{name:minlength(1)}", async (string name, IMediator mediator, DateTime? utcStart = null, DateTime? utcEnd = null, string? organizationId = null)
            => (await mediator.InvokeAsync<Result>(new AdminRunMaintenance(name, utcStart, utcEnd, organizationId))).ToHttpResult());

        group.MapGet("elasticsearch", async (IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminElasticsearch())).ToHttpResult());

        group.MapGet("elasticsearch/snapshots", async (IMediator mediator)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminElasticsearchSnapshots())).ToHttpResult());

        group.MapPost("generate-sample-events", async (IMediator mediator, int eventCount = 250, int daysBack = 7)
            => (await mediator.InvokeAsync<Result<object>>(new AdminGenerateSampleEvents(eventCount, daysBack))).ToHttpResult());

        return endpoints;
    }
}
