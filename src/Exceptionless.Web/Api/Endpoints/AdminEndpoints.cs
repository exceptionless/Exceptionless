using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Assistant;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.Admin;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Mvc;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2/admin")
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .ExcludeFromDescription();

        group.MapGet("echo", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper)
            => (await mediator.InvokeAsync<Result<object>>(new GetAdminEcho(httpContext))).ToHttpResult(resultMapper));

        group.MapGet("assistant-settings", async (AssistantModelSettingsService settingsService)
            => HttpResults.Ok(await settingsService.GetAsync()));

        group.MapPut("assistant-settings", async (HttpContext httpContext, [FromBody] UpdateAssistantSettings request, AssistantModelSettingsService settingsService)
            => HttpResults.Ok(await settingsService.SetModelAsync(request.Model, httpContext.Request.GetUser().Id)));

        endpoints.MapGet("api/v2/admin/assistant-usage", GetAssistantUsageAsync)
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Produces<AdminAssistantUsageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("change-plan", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string organizationId, string planId)
            => (await mediator.InvokeAsync<Result<object>>(new AdminChangePlan(organizationId, planId, httpContext))).ToHttpResult(resultMapper));

        group.MapPost("set-bonus", async (HttpContext httpContext, IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, string organizationId, int bonusEvents, DateTime? expires = null)
            => (await mediator.InvokeAsync<Result>(new AdminSetBonus(organizationId, bonusEvents, expires, httpContext))).ToHttpResult(resultMapper));

        group.MapPost("generate-sample-events", async (IMediator mediator, IMediatorResultMapper<HttpIResult> resultMapper, int eventCount = 250, int daysBack = 7)
            => (await mediator.InvokeAsync<Result<object>>(new AdminGenerateSampleEvents(eventCount, daysBack))).ToHttpResult(resultMapper));

        return endpoints;
    }

    private static async Task<HttpIResult> GetAssistantUsageAsync(
        HttpContext httpContext,
        IMediator mediator,
        IMediatorResultMapper<HttpIResult> resultMapper,
        DateTime? month = null,
        int limit = 100)
        => (await mediator.InvokeAsync<Result<object>>(new GetAdminAssistantUsage(month, limit, httpContext))).ToHttpResult(resultMapper);
}
