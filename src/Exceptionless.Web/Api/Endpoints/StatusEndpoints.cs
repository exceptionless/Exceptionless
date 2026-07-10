using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
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

        group.MapPost("notifications/release", async (IMediator mediator, [FromBody] ValueFromBody<string> message, bool critical = false) =>
        {
            var result = await mediator.InvokeAsync<object>(new PostReleaseNotification(message.Value, critical));
            return HttpResults.Ok(result);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        group.MapGet("notifications/system", async (IMediator mediator) =>
        {
            var result = await mediator.InvokeAsync<SystemNotification>(new GetSystemNotification());
            return result.Date == DateTime.MinValue ? HttpResults.Ok() : HttpResults.Ok(result);
        });

        group.MapPost("notifications/system", async (IMediator mediator, [FromBody] SetSystemNotificationRequest request, bool publish = true) =>
        {
            if (String.IsNullOrWhiteSpace(request.Message))
                return HttpResults.NotFound();

            var result = await mediator.InvokeAsync<object>(new PostSystemNotification(request.Message, request.Level, request.Target, publish));
            return HttpResults.Ok(result);
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        group.MapDelete("notifications/system", async (IMediator mediator, bool publish = true) =>
        {
            await mediator.InvokeAsync(new RemoveSystemNotification(publish));
            return HttpResults.Ok();
        })
        .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy);

        return endpoints;
    }
}
