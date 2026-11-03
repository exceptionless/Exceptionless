using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using BulkOperationMessages = Exceptionless.Web.Api.Messages;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Endpoints;

public static class BulkOperationEndpoints
{
    public static IEndpointRouteBuilder MapBulkOperationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("BulkOperation");

        group.MapPost("bulk-operations", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] NewBulkOperation operation)
            => (await mediator.InvokeAsync<Result<AcceptedResponse<ViewBulkOperation>>>(new BulkOperationMessages.CreateBulkOperation(operation))).ToHttpResult(resultMapper))
        .Accepts<NewBulkOperation>("application/json", "application/*+json")
        .Produces<ViewBulkOperation>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status426UpgradeRequired)
        .WithSummary("Create")
        .WithDescription("""
            Starts an action, such as `events.delete`, on everything that matches a selection. The selection is either a list of ids
            or a filter with a time range. The time range is resolved to absolute UTC dates when the operation is created, and events
            stored after that moment are never included. The operation runs in the background; poll it or listen for
            `BulkOperationUpdated` messages to follow its progress.
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

        group.MapGet("bulk-operations/{id:objectid}", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewBulkOperation>>(new BulkOperationMessages.GetBulkOperationById(id))).ToHttpResult(resultMapper))
        .WithName("GetBulkOperationById")
        .Produces<ViewBulkOperation>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the bulk operation.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The bulk operation could not be found.",
            }
        });

        group.MapGet("organizations/{organizationId:objectid}/bulk-operations", async (string organizationId, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<IReadOnlyCollection<ViewBulkOperation>>>(new BulkOperationMessages.GetBulkOperationsByOrganization(organizationId))).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<ViewBulkOperation>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by organization")
        .WithDescription("Returns the active and recently finished bulk operations of the organization, newest first. Finished operations are kept for seven days.")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["organizationId"] = "The identifier of the organization.",
            },
            ResponseDescriptions = new() {
                ["404"] = "The organization could not be found.",
            }
        });

        group.MapPost("bulk-operations/{id:objectid}/cancel", async (string id, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<AcceptedResponse<ViewBulkOperation>>>(new BulkOperationMessages.CancelBulkOperation(id))).ToHttpResult(resultMapper))
        .Produces<ViewBulkOperation>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Cancel")
        .WithDescription("Requests cancellation. A running operation stops after the batch it is processing; work already done is not undone. Cancelling a finished operation has no effect.")
        .WithMetadata(new EndpointDocumentation {
            ParameterDescriptions = new() {
                ["id"] = "The identifier of the bulk operation.",
            },
            ResponseDescriptions = new() {
                ["202"] = "Accepted",
                ["404"] = "The bulk operation could not be found.",
            }
        });

        return endpoints;
    }
}
