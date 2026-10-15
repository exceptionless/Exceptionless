using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using RateNotificationMessages = Exceptionless.Web.Api.Messages;

namespace Exceptionless.Web.Api.Endpoints;

public static class RateNotificationEndpoints
{
    public static IEndpointRouteBuilder MapRateNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/users/{userId:objectid}/projects/{projectId:objectid}/rate-notifications")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .WithTags("RateNotification");

        group.MapGet("", async (string userId, string projectId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, int page = 1, int limit = 25)
            => (await mediator.InvokeAsync<Result<PagedResult<ViewRateNotificationRule>>>(new RateNotificationMessages.GetRateNotifications(userId, projectId, page, limit, context))).ToHttpResult(resultMapper))
        .Produces<IReadOnlyCollection<ViewRateNotificationRule>>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get all")
        .WithMetadata(new EndpointDocumentation
        {
            ParameterDescriptions = new()
            {
                ["userId"] = "The identifier of the user.",
                ["projectId"] = "The identifier of the project.",
                ["page"] = "The page number.",
                ["limit"] = "The maximum number of rules to return."
            }
        });

        group.MapPost("", async (string userId, string projectId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] NewRateNotificationRule rule)
            => (await mediator.InvokeAsync<Result<ViewRateNotificationRule>>(new RateNotificationMessages.CreateRateNotification(userId, projectId, rule, context))).ToHttpResult(resultMapper))
        .Accepts<NewRateNotificationRule>("application/json", "application/*+json")
        .Produces<ViewRateNotificationRule>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Create");

        group.MapGet("{ruleId:objectid}", async (string userId, string projectId, string ruleId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewRateNotificationRule>>(new RateNotificationMessages.GetRateNotificationById(userId, projectId, ruleId, context))).ToHttpResult(resultMapper))
        .WithName("GetRateNotificationRuleById")
        .Produces<ViewRateNotificationRule>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get by id");

        group.MapPut("{ruleId:objectid}", async (string userId, string projectId, string ruleId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] UpdateRateNotificationRule rule)
            => (await mediator.InvokeAsync<Result<ViewRateNotificationRule>>(new RateNotificationMessages.UpdateRateNotification(userId, projectId, ruleId, rule, context))).ToHttpResult(resultMapper))
        .Accepts<UpdateRateNotificationRule>("application/json", "application/*+json")
        .Produces<ViewRateNotificationRule>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Update");

        group.MapDelete("{ruleId:objectid}", async (string userId, string projectId, string ruleId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result>(new RateNotificationMessages.DeleteRateNotification(userId, projectId, ruleId, context))).ToHttpResult(resultMapper))
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Remove");

        group.MapPost("{ruleId:objectid}/snooze", async (string userId, string projectId, string ruleId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, [FromBody] SnoozeRateNotificationRuleRequest request)
            => (await mediator.InvokeAsync<Result<ViewRateNotificationRule>>(new RateNotificationMessages.SnoozeRateNotification(userId, projectId, ruleId, request, context))).ToHttpResult(resultMapper))
        .Accepts<SnoozeRateNotificationRuleRequest>("application/json", "application/*+json")
        .Produces<ViewRateNotificationRule>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .WithSummary("Snooze");

        group.MapPost("{ruleId:objectid}/unsnooze", async (string userId, string projectId, string ruleId, HttpContext context, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<ViewRateNotificationRule>>(new RateNotificationMessages.UnsnoozeRateNotification(userId, projectId, ruleId, context))).ToHttpResult(resultMapper))
        .Produces<ViewRateNotificationRule>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Unsnooze");

        return endpoints;
    }
}
