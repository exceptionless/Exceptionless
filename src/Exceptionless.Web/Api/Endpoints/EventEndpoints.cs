using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Repositories.Models;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Events");

        // Count
        group.MapGet("events/count", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? aggregations = null, string? time = null, string? offset = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventCount(filter, aggregations, time, offset, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<CountResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Count")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["aggregations"] = "A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
            }
        });

        group.MapGet("organizations/{organizationId:objectid}/events/count", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? aggregations = null, string? time = null, string? offset = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventCountByOrganization(organizationId, filter, aggregations, time, offset, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<CountResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Count by organization")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["aggregations"] = "A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
            }
        });

        group.MapGet("projects/{projectId:objectid}/events/count", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? aggregations = null, string? time = null, string? offset = null, string? mode = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventCountByProject(projectId, filter, aggregations, time, offset, mode, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<CountResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Count by project")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["aggregations"] = "A list of values you want returned. Example: avg:value cardinality:value sum:users max:value min:value",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If mode is set to stack_new, then additional filters will be added.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
            }
        });

        // Get by id
        group.MapGet("events/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? time = null, string? offset = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventById(id, time, offset, httpContext)))
        .WithName("GetPersistentEventById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<PersistentEvent>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the event.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The event occurrence could not be found.",
                ["426"] = "Unable to view event occurrence due to plan limits.",
            }
        });

        // Get all
        group.MapGet("events", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAllEvents(filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Get by organization
        group.MapGet("organizations/{organizationId:objectid}/events", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByOrganization(organizationId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get by organization")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The organization could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Get by project
        group.MapGet("projects/{projectId:objectid}/events", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByProject(projectId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get by project")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The project could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Get by stack
        group.MapGet("stacks/{stackId:objectid}/events", async (string stackId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByStack(stackId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get by stack")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["stackId"] = "The identifier of the stack.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The stack could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Get by reference id
        group.MapGet("events/by-ref/{referenceId:identifier}", async (string referenceId, HttpContext httpContext, IMediator mediator, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByReferenceId(referenceId, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get by reference id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["referenceId"] = "An identifier used that references an event instance.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Get by reference id + project
        group.MapGet("projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}", async (string referenceId, string projectId, HttpContext httpContext, IMediator mediator, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsByReferenceIdAndProject(referenceId, projectId, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get by reference id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["referenceId"] = "An identifier used that references an event instance.",
                ["projectId"] = "The identifier of the project.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The project could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Sessions by session id
        group.MapGet("events/sessions/{sessionId:identifier}", async (string sessionId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsBySessionId(sessionId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get a list of all sessions or events by a session id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["sessionId"] = "An identifier that represents a session of events.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Sessions by session id + project
        group.MapGet("projects/{projectId:objectid}/events/sessions/{sessionId:identifier}", async (string sessionId, string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetEventsBySessionIdAndProject(sessionId, projectId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get a list of by a session id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["sessionId"] = "An identifier that represents a session of events.",
                ["projectId"] = "The identifier of the project.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The project could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // All sessions
        group.MapGet("events/sessions", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetSessions(filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get a list of all sessions")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
            }
        });

        // Sessions by organization
        group.MapGet("organizations/{organizationId:objectid}/events/sessions", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetSessionsByOrganization(organizationId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get a list of all sessions")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The project could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // Sessions by project
        group.MapGet("projects/{projectId:objectid}/events/sessions", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int? page = null, int limit = 10, string? before = null, string? after = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetSessionsByProject(projectId, filter, sort, time, offset, mode, page, limit, before, after, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Get a list of all sessions")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole event object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
                ["before"] = "The before parameter is a cursor used for pagination and defines your place in the list of results.",
                ["after"] = "The after parameter is a cursor used for pagination and defines your place in the list of results.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The project could not be found.",
                ["426"] = "Unable to view event occurrences for the suspended organization.",
            }
        });

        // User description
        group.MapPost("events/by-ref/{referenceId:identifier}/user-description", async (string referenceId, HttpContext httpContext, IMediator mediator, [FromBody] UserDescription description, string? projectId = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SetEventUserDescription(referenceId, description, projectId, httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Accepts<UserDescription>("application/json")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Set user description")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["referenceId"] = "An identifier used that references an event instance.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "Description must be specified.",
                ["404"] = "The event occurrence with the specified reference id could not be found.",
            }
        });

        group.MapPost("projects/{projectId:objectid}/events/by-ref/{referenceId:identifier}/user-description", async (string referenceId, string projectId, HttpContext httpContext, IMediator mediator, [FromBody] UserDescription description)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SetEventUserDescription(referenceId, description, projectId, httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Accepts<UserDescription>("application/json")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Set user description")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["referenceId"] = "An identifier used that references an event instance.",
                ["projectId"] = "The identifier of the project.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "Description must be specified.",
                ["404"] = "The event occurrence with the specified reference id could not be found.",
            }
        });

        // Legacy patch (v1)
        endpoints.MapPatch("api/v1/error/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] Delta<UpdateEvent> changes)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new LegacyPatchEvent(id, changes, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .WithMetadata(new ObsoleteAttribute("Use PATCH /api/v2/events"));

        // Heartbeat
        group.MapGet("events/session/heartbeat", async (HttpContext httpContext, IMediator mediator, string? id = null, bool close = false)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RecordEventHeartbeat(id, close, httpContext)))
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit heartbeat")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The session id or user id.",
                ["close"] = "If true, the session will be closed.",
            },
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        // Submit via GET - v1 legacy
        endpoints.MapGet("api/v1/events/submit", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 1, null, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use GET /api/v2/events/submit"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        endpoints.MapGet("api/v1/events/submit/{type:minlength(1)}", async (string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 1, type, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use GET /api/v2/events/submit"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        endpoints.MapGet("api/v1/projects/{projectId:objectid}/events/submit", async (string projectId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 1, null, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use GET /api/v2/events/submit"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        endpoints.MapGet("api/v1/projects/{projectId:objectid}/events/submit/{type:minlength(1)}", async (string projectId, string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 1, type, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use GET /api/v2/events/submit"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        // Submit via GET - v2
        group.MapGet("events/submit", async (HttpContext httpContext, IMediator mediator, string? type = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 2, type, httpContext.Request.GetClientUserAgent(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit event by GET")
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ParameterDescriptions = new() {
                ["type"] = "The event type (ie. error, log message, feature usage).",
                ["source"] = "The event source (ie. machine name, log name, feature name).",
                ["message"] = "The event message.",
                ["reference"] = "An optional identifier to be used for referencing this event instance at a later time.",
                ["date"] = "The date that the event occurred on.",
                ["count"] = "The number of duplicated events.",
                ["value"] = "The value of the event if any.",
                ["geo"] = "The geo coordinates where the event happened.",
                ["tags"] = "A list of tags used to categorize this event (comma separated).",
                ["identity"] = "The user's identity that the event happened to.",
                ["identityname"] = "The user's friendly name that the event happened to.",
                ["userAgent"] = "The user agent that submitted the event.",
                ["parameters"] = "Query string parameters that control what properties are set on the event",
            },
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        group.MapGet("events/submit/{type:minlength(1)}", async (string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(null, 2, type, httpContext.Request.GetClientUserAgent(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit event type by GET")
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ParameterDescriptions = new() {
                ["type"] = "The event type (ie. error, log message, feature usage).",
                ["source"] = "The event source (ie. machine name, log name, feature name).",
                ["message"] = "The event message.",
                ["reference"] = "An optional identifier to be used for referencing this event instance at a later time.",
                ["date"] = "The date that the event occurred on.",
                ["count"] = "The number of duplicated events.",
                ["value"] = "The value of the event if any.",
                ["geo"] = "The geo coordinates where the event happened.",
                ["tags"] = "A list of tags used to categorize this event (comma separated).",
                ["identity"] = "The user's identity that the event happened to.",
                ["identityname"] = "The user's friendly name that the event happened to.",
                ["userAgent"] = "The user agent that submitted the event.",
                ["parameters"] = "Query string parameters that control what properties are set on the event",
            },
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        group.MapGet("projects/{projectId:objectid}/events/submit", async (string projectId, HttpContext httpContext, IMediator mediator, string? type = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 2, type, httpContext.Request.GetClientUserAgent(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit event type by GET for a specific project")
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["source"] = "The event source (ie. machine name, log name, feature name).",
                ["message"] = "The event message.",
                ["reference"] = "An optional identifier to be used for referencing this event instance at a later time.",
                ["date"] = "The date that the event occurred on.",
                ["count"] = "The number of duplicated events.",
                ["value"] = "The value of the event if any.",
                ["geo"] = "The geo coordinates where the event happened.",
                ["tags"] = "A list of tags used to categorize this event (comma separated).",
                ["identity"] = "The user's identity that the event happened to.",
                ["identityname"] = "The user's friendly name that the event happened to.",
                ["userAgent"] = "The user agent that submitted the event.",
                ["parameters"] = "Query String parameters that control what properties are set on the event",
            },
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        group.MapGet("projects/{projectId:objectid}/events/submit/{type:minlength(1)}", async (string projectId, string type, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByGet(projectId, 2, type, httpContext.Request.GetClientUserAgent(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit event type by GET for a specific project")
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.SubmitGetAdditionalParameters,
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["type"] = "The event type (ie. error, log message, feature usage).",
                ["source"] = "The event source (ie. machine name, log name, feature name).",
                ["message"] = "The event message.",
                ["reference"] = "An optional identifier to be used for referencing this event instance at a later time.",
                ["date"] = "The date that the event occurred on.",
                ["count"] = "The number of duplicated events.",
                ["value"] = "The value of the event if any.",
                ["geo"] = "The geo coordinates where the event happened.",
                ["tags"] = "A list of tags used to categorize this event (comma separated).",
                ["identity"] = "The user's identity that the event happened to.",
                ["identityname"] = "The user's friendly name that the event happened to.",
                ["userAgent"] = "The user agent that submitted the event.",
                ["parameters"] = "Query String parameters that control what properties are set on the event",
            },
            ResponseDescriptions = new() {
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        // Submit via POST - v1 legacy
        endpoints.MapPost("api/v1/error", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(null, 1, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use POST /api/v2/events"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.PostUserAgentParameter,
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        endpoints.MapPost("api/v1/events", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(null, 1, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use POST /api/v2/events"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.PostUserAgentParameter,
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        endpoints.MapPost("api/v1/projects/{projectId:objectid}/events", async (string projectId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(projectId, 1, httpContext.Request.GetClientUserAgent(), httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithMetadata(new ObsoleteAttribute("Use POST /api/v2/events"))
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.PostUserAgentParameter,
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        // Submit via POST - v2
        group.MapPost("events", async (HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(null, 2, httpContext.Request.GetClientUserAgent(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit event by POST")
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.PostUserAgentParameter,
            ParameterDescriptions = new() {
                ["userAgent"] = "The user agent that submitted the event.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        group.MapPost("projects/{projectId:objectid}/events", async (string projectId, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SubmitEventByPost(projectId, 2, httpContext.Request.GetClientUserAgent(), httpContext)))
        .AddEndpointFilter<ConfigurationResponseEndpointFilter>()
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Submit event by POST for a specific project")
        .WithMetadata(new EndpointDocumentation {
            AdditionalParameters = EventEndpointHelpers.PostUserAgentParameter,
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["userAgent"] = "The user agent that submitted the event.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "No project id specified and no default project was found.",
                ["404"] = "No project was found.",
            }
        });

        // Delete
        group.MapDelete("events/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new DeleteEvents(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Produces<WorkInProgressResult>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of event identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more event occurrences were not found.",
                ["500"] = "An error occurred while deleting one or more event occurrences.",
            }
        });

        return endpoints;
    }
}

internal static class EventEndpointHelpers
{
    /// <summary>
    /// Additional parameters for all event submit GET endpoints.
    /// These are read from HttpContext/query string rather than method parameters.
    /// </summary>
    public static readonly List<AdditionalParameterDefinition> SubmitGetAdditionalParameters =
    [
        new("source", "query", Description: "The event source (ie. machine name, log name, feature name)."),
        new("message", "query", Description: "The event message."),
        new("reference", "query", Description: "An optional identifier to be used for referencing this event instance at a later time."),
        new("date", "query", Description: "The date that the event occurred on."),
        new("count", "query", Description: "The number of duplicated events.", Type: "integer", Format: "int32"),
        new("value", "query", Description: "The value of the event if any.", Type: "number", Format: "double"),
        new("geo", "query", Description: "The geo coordinates where the event happened."),
        new("tags", "query", Description: "A list of tags used to categorize this event (comma separated)."),
        new("identity", "query", Description: "The user's identity that the event happened to."),
        new("identityname", "query", Description: "The user's friendly name that the event happened to."),
        new("userAgent", "header", Description: "The user agent that submitted the event."),
        new("parameters", "query", Description: "Query string parameters that control what properties are set on the event", Type: "array"),
    ];

    /// <summary>
    /// Additional parameters for POST event endpoints (just userAgent header).
    /// </summary>
    public static readonly List<AdditionalParameterDefinition> PostUserAgentParameter =
    [
        new("userAgent", "header", Description: "The user agent that submitted the event."),
    ];
}
