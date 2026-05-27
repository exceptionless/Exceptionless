using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models.Data;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .WithTags("Events");

        // Count
        group.MapGet("events/count", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? aggregations = null, string? time = null, string? offset = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventCount(filter, aggregations, time, offset, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapGet("organizations/{organizationId:objectid}/events/count", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? aggregations = null, string? time = null, string? offset = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventCountByOrganization(organizationId, filter, aggregations, time, offset, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        group.MapGet("projects/{projectId:objectid}/events/count", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? aggregations = null, string? time = null, string? offset = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventCountByProject(projectId, filter, aggregations, time, offset, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by id
        group.MapGet("events/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? time = null, string? offset = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventById(id, time, offset, httpContext)))
        .WithName("GetPersistentEventById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get all
        group.MapGet("events", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAllEvents(filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by organization
        group.MapGet("organizations/{organizationId:objectid}/events", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByOrganization(organizationId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by project
        group.MapGet("projects/{projectId:objectid}/events", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByProject(projectId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by stack
        group.MapGet("stacks/{stackId:objectid}/events", async (string stackId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByStack(stackId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by reference id
        group.MapGet("events/by-ref/{referenceId}", async (string referenceId, HttpContext httpContext, IMediator mediator, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByReferenceId(referenceId, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Get by reference id + project
        group.MapGet("projects/{projectId:objectid}/events/by-ref/{referenceId}", async (string referenceId, string projectId, HttpContext httpContext, IMediator mediator, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByReferenceIdAndProject(referenceId, projectId, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Sessions by session id
        group.MapGet("events/sessions/{sessionId}", async (string sessionId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsBySessionId(sessionId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Sessions by session id + project
        group.MapGet("projects/{projectId:objectid}/events/sessions/{sessionId}", async (string sessionId, string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsBySessionIdAndProject(sessionId, projectId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // All sessions
        group.MapGet("events/sessions", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetSessions(filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Sessions by organization
        group.MapGet("organizations/{organizationId:objectid}/events/sessions", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetSessionsByOrganization(organizationId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // Sessions by project
        group.MapGet("projects/{projectId:objectid}/events/sessions", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetSessionsByProject(projectId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        // User description
        group.MapPost("events/by-ref/{referenceId}/user-description", async (string referenceId, HttpContext httpContext, IMediator mediator, [FromBody] UserDescription description, string? projectId = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SetEventUserDescription(referenceId, description, projectId, httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Accepts<UserDescription>("application/json");

        group.MapPost("projects/{projectId:objectid}/events/by-ref/{referenceId}/user-description", async (string referenceId, string projectId, HttpContext httpContext, IMediator mediator, [FromBody] UserDescription description)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SetEventUserDescription(referenceId, description, projectId, httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Accepts<UserDescription>("application/json");

        // Legacy patch (v1)
        endpoints.MapPatch("api/v1/error/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<UpdateEvent> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new LegacyPatchEvent(id, changes, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        // Heartbeat
        group.MapGet("events/session/heartbeat", async (HttpContext httpContext, IMediator mediator, string? id = null, bool close = false)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RecordEventHeartbeat(id, close, httpContext)));

        // Submit via GET - v1 legacy
        endpoints.MapGet("api/v1/events/submit", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 1, null, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        endpoints.MapGet("api/v1/events/submit/{type}", async (string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 1, type, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        endpoints.MapGet("api/v1/projects/{projectId:objectid}/events/submit", async (string projectId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 1, null, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        endpoints.MapGet("api/v1/projects/{projectId:objectid}/events/submit/{type}", async (string projectId, string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 1, type, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        // Submit via GET - v2
        group.MapGet("events/submit", async (HttpContext httpContext, IMediator mediator, string? type = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 2, type, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>();

        group.MapGet("events/submit/{type}", async (string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 2, type, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>();

        group.MapGet("projects/{projectId:objectid}/events/submit", async (string projectId, HttpContext httpContext, IMediator mediator, string? type = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 2, type, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>();

        group.MapGet("projects/{projectId:objectid}/events/submit/{type}", async (string projectId, string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 2, type, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>();

        // Submit via POST - v1 legacy
        endpoints.MapPost("api/v1/error", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(null, 1, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/events", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(null, 1, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        endpoints.MapPost("api/v1/projects/{projectId:objectid}/events", async (string projectId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(projectId, 1, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .ExcludeFromDescription();

        // Submit via POST - v2
        group.MapPost("events", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(null, 2, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>();

        group.MapPost("projects/{projectId:objectid}/events", async (string projectId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(projectId, 2, httpContext.Request.Headers[HeaderNames.UserAgent].ToString(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>();

        // Delete
        group.MapDelete("events/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new DeleteEvents(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy);

        return endpoints;
    }
}
