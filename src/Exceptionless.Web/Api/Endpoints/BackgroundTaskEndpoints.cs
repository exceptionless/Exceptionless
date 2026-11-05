using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using TaskMessages = Exceptionless.Web.Api.Messages;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Endpoints;

public static class BackgroundTaskEndpoints
{
    public static IEndpointRouteBuilder MapBackgroundTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("BackgroundTask");

        group.MapPost("bulk-operations", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] NewBulkOperation operation)
            => (await mediator.InvokeAsync<Result<AcceptedResponse<ViewBackgroundTask>>>(new TaskMessages.CreateBulkOperation(operation))).ToHttpResult(resultMapper))
        .Accepts<NewBulkOperation>("application/json", "application/*+json")
        .Produces<ViewBackgroundTask>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Create")
        .WithDescription("""
            Starts an action, such as `events.delete`, on everything that matches a selection. The selection is either a list of ids
            or a filter with a time range. The time range is resolved to absolute UTC dates when the operation is created, and events
            stored after that moment are never included. The operation runs in the background and is returned as a task; poll
            `GET api/v2/tasks/{id}` or listen for `BackgroundTaskUpdated` messages to follow its progress.
            """)
        .WithMetadata(new EndpointDocumentation {
            RequestBodyDescription = "The bulk operation to start.",
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["403"] = "The action is not permitted for this organization or project.",
                ["404"] = "The organization or project could not be found.",
                ["409"] = "A destructive operation is already running for this project.",
                ["422"] = "The request is not valid.",
                ["426"] = "The filter uses a feature that requires a plan upgrade.",
            }
        });

        group.MapGet("tasks/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewBackgroundTask>>(new TaskMessages.GetBackgroundTaskById(id))).ToHttpResult(resultMapper))
        .WithName("GetBackgroundTaskById")
        .Produces<ViewBackgroundTask>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithDescription("Returns the progress of a task, such as a bulk action, a project data reset or sample data generation. Tasks are kept for seven days.")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the task.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The task could not be found.",
            }
        });

        group.MapGet("organizations/{organizationId:objectid}/tasks", async (string organizationId, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<ViewBackgroundTask>>>(new TaskMessages.GetBackgroundTasksByOrganization(organizationId))).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<ViewBackgroundTask>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by organization")
        .WithDescription("Returns the active and recently finished tasks of the organization, newest first. Finished tasks are kept for seven days.")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPost("tasks/{id:objectid}/cancel", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<AcceptedResponse<ViewBackgroundTask>>>(new TaskMessages.CancelBackgroundTask(id))).ToHttpResult(resultMapper))
        .Produces<ViewBackgroundTask>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Cancel")
        .WithDescription("Requests cancellation. A running bulk action stops after the batch it is processing; work already done is not undone. Cancelling a finished task has no effect. Tasks that cannot be stopped, such as a project data reset, are rejected.")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the task.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The task could not be found.",
                ["422"] = "The task cannot be cancelled.",
            }
        });

        return endpoints;
    }
}
