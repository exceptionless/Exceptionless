using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Models;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using WebHookMessages = Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Endpoints;

public static class WebHookEndpoints
{
    public static IEndpointRouteBuilder MapWebHookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>();

        group.MapGet("projects/{projectId:objectid}/webhooks", async (string projectId, IMediator mediator, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.GetWebHooksByProject(projectId, page, limit)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapGet("webhooks/{id:objectid}", async (string id, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.GetWebHookById(id)))
        .WithName("GetWebHookById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapPost("webhooks", async (IMediator mediator, IServiceProvider serviceProvider, [FromBody] NewWebHook webHook) =>
        {
            var validation = await ApiValidation.ValidateAsync(webHook, serviceProvider);
            if (validation is not null)
                return validation;

            return await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.CreateWebHook(webHook));
        })
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapDelete("webhooks/{ids:objectids}", async (string ids, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.DeleteWebHooks(ids.FromDelimitedString())))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapPost("webhooks/subscribe", async (IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.SubscribeWebHook(data, 1)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        endpoints.MapPost("api/v{apiVersion:int}/webhooks/subscribe", async (int apiVersion, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.SubscribeWebHook(data, apiVersion)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        group.MapPost("webhooks/unsubscribe", async (IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.UnsubscribeWebHook(data)))
        .AllowAnonymous()
        .ExcludeFromDescription();

        group.MapGet("webhooks/test", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.TestWebHook()))
        .ExcludeFromDescription();

        group.MapPost("webhooks/test", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.TestWebHook()))
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projecthook/subscribe", async (IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.SubscribeWebHook(data, 1)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projecthook/unsubscribe", async (IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.UnsubscribeWebHook(data)))
        .AllowAnonymous()
        .ExcludeFromDescription();

        endpoints.MapGet("api/v1/projecthook/test", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.TestWebHook()))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projecthook/test", async (IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new WebHookMessages.TestWebHook()))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        return endpoints;
    }
}
