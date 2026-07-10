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

        group.MapGet("settings", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminSettings())).ToHttpResult(resultMapper));

        group.MapGet("stats", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminStats())).ToHttpResult(resultMapper));

        group.MapGet("migrations", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminMigrations())).ToHttpResult(resultMapper));

        group.MapGet("echo", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminEcho(httpContext))).ToHttpResult(resultMapper));

        group.MapGet("assemblies", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminAssemblies())).ToHttpResult(resultMapper));

        group.MapPost("change-plan", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string organizationId, string planId)
            => (await mediator.InvokeAsync<Result<object>>(new AdminChangePlan(organizationId, planId, httpContext))).ToHttpResult(resultMapper));

        group.MapPost("set-bonus", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string organizationId, int bonusEvents, DateTime? expires = null)
            => (await mediator.InvokeAsync<Result>(new AdminSetBonus(organizationId, bonusEvents, expires, httpContext))).ToHttpResult(resultMapper));

        group.MapGet("requeue", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, string? path = null, bool archive = false)
            => (await mediator.InvokeAsync<Result<object>>(new AdminRequeue(path, archive))).ToHttpResult(resultMapper));

        group.MapGet("maintenance/{name:minlength(1)}", async (string name, IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, DateTime? utcStart = null, DateTime? utcEnd = null, string? organizationId = null)
            => (await mediator.InvokeAsync<Result>(new AdminRunMaintenance(name, utcStart, utcEnd, organizationId))).ToHttpResult(resultMapper));

        group.MapGet("elasticsearch", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminElasticsearch())).ToHttpResult(resultMapper));

        group.MapGet("elasticsearch/snapshots", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminElasticsearchSnapshots())).ToHttpResult(resultMapper));

        group.MapPost("generate-sample-events", async (IMediator mediator, IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult> resultMapper, int eventCount = 250, int daysBack = 7)
            => (await mediator.InvokeAsync<Result<object>>(new AdminGenerateSampleEvents(eventCount, daysBack))).ToHttpResult(resultMapper));

        return endpoints;
    }
}
