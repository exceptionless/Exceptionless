using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Services;
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

        endpoints.MapGet("api/v2/admin/assistant-settings", async (AssistantModelSettingsService settingsService)
            => HttpResults.Ok(await settingsService.GetAsync()))
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Produces<AssistantModelSettings>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags(nameof(AdminEndpoints))
            .WithSummary("Get Exie assistant settings");

        endpoints.MapPut("api/v2/admin/assistant-settings", async (HttpContext httpContext, [FromBody] UpdateAssistantSettings request, AssistantModelSettingsService settingsService)
            => HttpResults.Ok(await settingsService.SetModelAsync(request.Model, httpContext.Request.GetUser().Id)))
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Accepts<UpdateAssistantSettings>("application/json", "application/*+json")
            .Produces<AssistantModelSettings>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithTags(nameof(AdminEndpoints))
            .WithSummary("Update Exie assistant settings");

        endpoints.MapPut("api/v2/admin/assistant-settings/enabled", async (HttpContext httpContext, [FromBody] UpdateAssistantEnabledSettings request, AssistantModelSettingsService settingsService)
            => HttpResults.Ok(await settingsService.SetEnabledAsync(request.Enabled, httpContext.Request.GetUser().Id)))
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Accepts<UpdateAssistantEnabledSettings>("application/json", "application/*+json")
            .Produces<AssistantModelSettings>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithTags(nameof(AdminEndpoints))
            .WithSummary("Update Exie assistant availability");

        endpoints.MapGet("api/v2/admin/event-submission-settings", GetEventSubmissionSettingsAsync)
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .Produces<EventSubmissionSettings>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags(nameof(AdminEndpoints))
            .WithSummary("Get event submission settings");

        endpoints.MapPut("api/v2/admin/event-submission-settings", UpdateEventSubmissionSettingsAsync)
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Accepts<UpdateEventSubmissionSettings>("application/json", "application/*+json")
            .Produces<EventSubmissionSettings>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .WithTags(nameof(AdminEndpoints))
            .WithSummary("Update event submission settings");

        endpoints.MapGet("api/v2/admin/assistant-usage", GetAssistantUsageAsync)
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Produces<AdminAssistantUsageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("api/v2/admin/product-tour-usage", GetProductTourUsageAsync)
            .RequireAuthorization(AuthorizationRoles.GlobalAdminPolicy)
            .AddEndpointFilter<AutoValidationEndpointFilter>()
            .Produces<ProductTourUsageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
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

    private static async Task<HttpIResult> GetEventSubmissionSettingsAsync(SystemSettingsService settingsService, AppOptions appOptions)
    {
        var settings = await settingsService.GetAsync();
        return HttpResults.Ok(CreateEventSubmissionSettings(settings?.EventSubmissionEnabled, appOptions));
    }

    private static async Task<HttpIResult> UpdateEventSubmissionSettingsAsync(
        HttpContext httpContext,
        [FromBody] UpdateEventSubmissionSettings request,
        SystemSettingsService settingsService,
        AppOptions appOptions)
    {
        bool configuredEnabled = !appOptions.EventSubmissionDisabled;
        bool? enabledOverride = request.Enabled == configuredEnabled ? null : request.Enabled;
        var settings = await settingsService.UpdateAsync(httpContext.Request.GetUser().Id, value => value.EventSubmissionEnabled = enabledOverride);
        return HttpResults.Ok(CreateEventSubmissionSettings(settings.EventSubmissionEnabled, appOptions));
    }

    private static EventSubmissionSettings CreateEventSubmissionSettings(bool? enabledOverride, AppOptions appOptions)
    {
        bool configuredEnabled = !appOptions.EventSubmissionDisabled;
        return new EventSubmissionSettings(enabledOverride ?? configuredEnabled, configuredEnabled, enabledOverride.HasValue);
    }

    private static async Task<HttpIResult> GetProductTourUsageAsync(
        IMediator mediator,
        IMediatorResultMapper<HttpIResult> resultMapper,
        DateTime? month = null,
        bool all = false,
        int limit = 100)
        => (await mediator.InvokeAsync<Result<object>>(new GetAdminProductTourUsage(month, all, limit))).ToHttpResult(resultMapper);
}
