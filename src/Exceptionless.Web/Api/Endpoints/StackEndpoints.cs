using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Api.Endpoints;

public static class StackEndpoints
{
    public static IEndpointRouteBuilder MapStackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .WithTags("Stacks");

        // GET by id
        group.MapGet("stacks/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? offset = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetStackById(id, offset, httpContext)))
        .WithName("GetStackById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Mark fixed
        group.MapPost("stacks/{ids:objectids}/mark-fixed", async (string ids, HttpContext httpContext, IMediator mediator, string? version = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksFixed(ids, version, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Mark fixed - Zapier legacy v1
        endpoints.MapPost("api/v1/stack/markfixed", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksFixedByZapier(data, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        // Mark fixed - Zapier v2 (no id in route)
        group.MapPost("stacks/mark-fixed", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksFixedByZapier(data, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .ExcludeFromDescription();

        // Snooze
        group.MapPost("stacks/{ids:objectids}/mark-snoozed", async (string ids, HttpContext httpContext, IMediator mediator, DateTime snoozeUntilUtc)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SnoozeStacks(ids, snoozeUntilUtc, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Add link
        group.MapPost("stacks/{id:objectid}/add-link", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string?> url)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AddStackLink(id, url, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string?>>("application/json");

        // Add link - Zapier legacy v1
        endpoints.MapPost("api/v1/stack/addlink", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AddStackLinkByZapier(data, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        // Add link - Zapier v2 (no id in route)
        group.MapPost("stacks/add-link", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AddStackLinkByZapier(data, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .ExcludeFromDescription();

        // Remove link
        group.MapPost("stacks/{id:objectid}/remove-link", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string> url)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RemoveStackLink(id, url, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string>>("application/json");

        // Mark critical
        group.MapPost("stacks/{ids:objectids}/mark-critical", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksCritical(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Mark not critical
        group.MapDelete("stacks/{ids:objectids}/mark-critical", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksNotCritical(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Change status
        group.MapPost("stacks/{ids:objectids}/change-status", async (string ids, HttpContext httpContext, IMediator mediator, StackStatus status)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ChangeStacksStatus(ids, status, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Promote
        group.MapPost("stacks/{id:objectid}/promote", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new PromoteStack(id, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Delete
        group.MapDelete("stacks/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new DeleteStacks(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get all
        group.MapGet("stacks", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAllStacks(filter, sort, time, offset, mode, page, limit, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by organization
        group.MapGet("organizations/{organizationId:objectid}/stacks", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetStacksByOrganization(organizationId, filter, sort, time, offset, mode, page, limit, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by project
        group.MapGet("projects/{projectId:objectid}/stacks", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetStacksByProject(projectId, filter, sort, time, offset, mode, page, limit, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        return endpoints;
    }
}
