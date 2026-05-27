using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class StatusEndpoints
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription();

        group.MapGet("about", async (IMediator mediator) =>
        {
            var result = await mediator.InvokeAsync<object>(new GetAboutInfo());
            return HttpResults.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetAboutInfo");

        group.MapGet("queue-stats", async (IMediator mediator) =>
        {
            var result = await mediator.InvokeAsync<object>(new GetQueueStats());
            return HttpResults.Ok(result);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        group.MapPost("notifications/release", async (IMediator mediator, [FromBody] ValueFromBody<string> message, bool critical = false) =>
        {
            var result = await mediator.InvokeAsync<object>(new PostReleaseNotification(message.Value, critical));
            return HttpResults.Ok(result);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        group.MapGet("notifications/system", async (IMediator mediator) =>
        {
            var result = await mediator.InvokeAsync<CacheValue<SystemNotification>>(new GetSystemNotification());
            return result.HasValue ? HttpResults.Ok(result.Value) : HttpResults.Ok();
        });

        group.MapPost("notifications/system", async (IMediator mediator, [FromBody] ValueFromBody<string> message) =>
        {
            if (String.IsNullOrWhiteSpace(message?.Value))
                return HttpResults.NotFound();

            var result = await mediator.InvokeAsync<object>(new PostSystemNotification(message.Value));
            return HttpResults.Ok(result);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        group.MapDelete("notifications/system", async (IMediator mediator) =>
        {
            await mediator.InvokeAsync(new RemoveSystemNotification());
            return HttpResults.Ok();
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        return endpoints;
    }
}
