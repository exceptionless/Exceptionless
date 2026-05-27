using System.Text.Json;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Models;
using IMediator = Foundatio.Mediator.IMediator;
using Microsoft.AspNetCore.Mvc;
using Exceptionless.Web.Utility.OpenApi;

namespace Exceptionless.Web.Api.Endpoints;

public static class StackEndpoints
{
    public static IEndpointRouteBuilder MapStackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("Stacks");

        // GET by id
        group.MapGet("stacks/{id:objectid}", async (string id, HttpContext httpContext, IMediator mediator, string? offset = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetStackById(id, offset, httpContext)))
        .WithName("GetStackById")
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the stack.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the `time` filter. This is used for time zone support.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The stack could not be found.",
            }
        });

        // Mark fixed
        group.MapPost("stacks/{ids:objectids}/mark-fixed", async (string ids, HttpContext httpContext, IMediator mediator, string? version = null)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksFixed(ids, version, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Mark fixed")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of stack identifiers.",
                ["version"] = "A version number that the stack was fixed in.",
            },
            ResponseDescriptions = new() {
                ["200"] = "The stacks were marked as fixed.",
                ["404"] = "One or more stacks could not be found.",
            }
        });

        // Mark fixed - Zapier legacy v1
        endpoints.MapPost("api/v1/stack/markfixed", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksFixedByZapier(data, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        // Mark fixed - Zapier v2 (no id in route)
        group.MapPost("stacks/mark-fixed", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksFixedByZapier(data, httpContext)))
        .ExcludeFromDescription();

        // Snooze
        group.MapPost("stacks/{ids:objectids}/mark-snoozed", async (string ids, HttpContext httpContext, IMediator mediator, DateTime snoozeUntilUtc)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new SnoozeStacks(ids, snoozeUntilUtc, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Mark the selected stacks as snoozed")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of stack identifiers.",
                ["snoozeUntilUtc"] = "A time that the stack should be snoozed until.",
            },
            ResponseDescriptions = new() {
                ["200"] = "The stacks were snoozed.",
                ["404"] = "One or more stacks could not be found.",
            }
        });

        // Add link
        group.MapPost("stacks/{id:objectid}/add-link", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string?> url)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AddStackLink(id, url, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string?>>("application/json")
        .WithSummary("Add reference link")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the stack.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid reference link.",
                ["404"] = "The stack could not be found.",
            }
        });

        // Add link - Zapier legacy v1
        endpoints.MapPost("api/v1/stack/addlink", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AddStackLinkByZapier(data, httpContext)))
        .RequireAuthorization(AuthorizationRoles.ClientPolicy)
        .ExcludeFromDescription();

        // Add link - Zapier v2 (no id in route)
        group.MapPost("stacks/add-link", async (HttpContext httpContext, IMediator mediator, [FromBody] JsonDocument data)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new AddStackLinkByZapier(data, httpContext)))
        .ExcludeFromDescription();

        // Remove link
        group.MapPost("stacks/{id:objectid}/remove-link", async (string id, HttpContext httpContext, IMediator mediator, [FromBody] ValueFromBody<string> url)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new RemoveStackLink(id, url, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .Accepts<ValueFromBody<string>>("application/json")
        .WithSummary("Remove reference link")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the stack.",
            },
            ResponseDescriptions = new() {
                ["204"] = "The reference link was removed.",
                ["400"] = "Invalid reference link.",
                ["404"] = "The stack could not be found.",
            }
        });

        // Mark critical
        group.MapPost("stacks/{ids:objectids}/mark-critical", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksCritical(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Mark future occurrences as critical")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of stack identifiers.",
            },
            ResponseDescriptions = new() {
                ["404"] = "One or more stacks could not be found.",
            }
        });

        // Mark not critical
        group.MapDelete("stacks/{ids:objectids}/mark-critical", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new MarkStacksNotCritical(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Mark future occurrences as not critical")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of stack identifiers.",
            },
            ResponseDescriptions = new() {
                ["204"] = "The stacks were marked as not critical.",
                ["404"] = "One or more stacks could not be found.",
            }
        });

        // Change status
        group.MapPost("stacks/{ids:objectids}/change-status", async (string ids, HttpContext httpContext, IMediator mediator, StackStatus status)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new ChangeStacksStatus(ids, status, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Change stack status")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of stack identifiers.",
                ["status"] = "The status that the stack should be changed to.",
            },
            ResponseDescriptions = new() {
                ["404"] = "One or more stacks could not be found.",
            }
        });

        // Promote
        group.MapPost("stacks/{id:objectid}/promote", async (string id, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new PromoteStack(id, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Promote to external service")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the stack.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The stack could not be found.",
                ["426"] = "Promote to External is a premium feature used to promote an error stack to an external system.",
                ["501"] = "No promoted web hooks are configured for this project.",
            }
        });

        // Delete
        group.MapDelete("stacks/{ids:objectids}", async (string ids, HttpContext httpContext, IMediator mediator)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new DeleteStacks(ids, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Remove")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["ids"] = "A comma-delimited list of stack identifiers.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["400"] = "One or more validation errors occurred.",
                ["404"] = "One or more stacks were not found.",
                ["500"] = "An error occurred while deleting one or more stacks.",
            }
        });

        // Get all
        group.MapGet("stacks", async (HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetAllStacks(filter, sort, time, offset, mode, page, limit, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole stack object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
            }
        });

        // Get by organization
        group.MapGet("organizations/{organizationId:objectid}/stacks", async (string organizationId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetStacksByOrganization(organizationId, filter, sort, time, offset, mode, page, limit, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Get by organization")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole stack object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The organization could not be found.",
                ["426"] = "Unable to view stack occurrences for the suspended organization.",
            }
        });

        // Get by project
        group.MapGet("projects/{projectId:objectid}/stacks", async (string projectId, HttpContext httpContext, IMediator mediator, string? filter = null, string? sort = null, string? time = null, string? offset = null, string? mode = null, int page = 1, int limit = 10)
            => await mediator.InvokeAsync<Microsoft.AspNetCore.Http.IResult>(new GetStacksByProject(projectId, filter, sort, time, offset, mode, page, limit, httpContext)))
        .RequireAuthorization(AuthorizationRoles.UserPolicy)
        .WithSummary("Get by project")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["projectId"] = "The identifier of the project.",
                ["filter"] = "A filter that controls what data is returned from the server.",
                ["sort"] = "Controls the sort order that the data is returned in. In this example -date returns the results descending by date.",
                ["time"] = "The time filter that limits the data being returned to a specific date range.",
                ["offset"] = "The time offset in minutes that controls what data is returned based on the time filter. This is used for time zone support.",
                ["mode"] = "If no mode is set then the whole stack object will be returned. If the mode is set to summary than a lightweight object will be returned.",
                ["page"] = "The page parameter is used for pagination. This value must be greater than 0.",
                ["limit"] = "A limit on the number of objects to be returned. Limit can range between 1 and 100 items.",
            },
            ResponseDescriptions = new() {
                ["400"] = "Invalid filter.",
                ["404"] = "The organization could not be found.",
                ["426"] = "Unable to view stack occurrences for the suspended organization.",
            }
        });

        return endpoints;
    }
}
