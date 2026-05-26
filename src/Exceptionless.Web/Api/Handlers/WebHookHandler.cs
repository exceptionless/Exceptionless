using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Repositories;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;

namespace Exceptionless.Web.Api.Handlers;

public class WebHookHandler(
    IWebHookRepository repository,
    IProjectRepository projectRepository,
    BillingManager billingManager,
    ApiMapper mapper,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<WebHookHandler>();
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<IResult> Handle(GetWebHooksByProject message)
    {
        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return HttpResults.NotFound();

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var results = await repository.GetByProjectIdAsync(message.ProjectId, o => o.PageNumber(page).PageLimit(limit));
        return ApiResults.OkWithResourceLinks(HttpContext, results.Documents.ToArray(), results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    public async Task<IResult> Handle(GetWebHookById message)
    {
        var model = await GetModelAsync(message.Id);
        return model is null ? HttpResults.NotFound() : HttpResults.Ok(model);
    }

    public Task<IResult> Handle(CreateWebHook message) => PostImplAsync(message.WebHook);

    public async Task<IResult> Handle(DeleteWebHooks message)
    {
        var items = await GetModelsAsync(message.Ids, useCache: false);
        if (items.Count == 0)
            return HttpResults.NotFound();

        var results = new ModelActionResults();
        results.AddNotFound(message.Ids.Except(items.Select(i => i.Id)));

        var deletableItems = items.ToList();
        foreach (var model in items)
        {
            var permission = await CanDeleteAsync(model);
            if (permission.Allowed)
                continue;

            deletableItems.Remove(model);
            results.Failure.Add(permission);
        }

        if (deletableItems.Count == 0)
            return results.Failure.Count == 1 ? PermissionToResult(results.Failure.First()) : HttpResults.BadRequest(results);

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return TypedResults.Json(new WorkInProgressResult(), statusCode: StatusCodes.Status202Accepted);

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return HttpResults.BadRequest(results);
    }

    public async Task<IResult> Handle(SubscribeWebHook message)
    {
        string? eventType = message.Data.RootElement.TryGetProperty("event", out var eventProp) ? eventProp.GetString() : null;
        string? url = message.Data.RootElement.TryGetProperty("target_url", out var urlProp) ? urlProp.GetString() : null;
        if (String.IsNullOrEmpty(eventType) || String.IsNullOrEmpty(url))
            return HttpResults.BadRequest();

        string? projectId = HttpContext.User.GetProjectId();
        if (projectId is null)
            return HttpResults.BadRequest();

        string? organizationId = HttpContext.Request.GetDefaultOrganizationId();
        if (organizationId is null)
            return HttpResults.BadRequest();

        var webHook = new NewWebHook
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            EventTypes = [eventType],
            Url = url,
            Version = new Version(message.ApiVersion >= 0 ? message.ApiVersion : 0, 0)
        };

        if (!webHook.Url.StartsWith("https://hooks.zapier.com", StringComparison.OrdinalIgnoreCase))
            return HttpResults.NotFound();

        return await PostImplAsync(webHook);
    }

    public async Task<IResult> Handle(UnsubscribeWebHook message)
    {
        string? targetUrl = message.Data.RootElement.TryGetProperty("target_url", out var urlProp) ? urlProp.GetString() : null;
        if (targetUrl is null || !targetUrl.StartsWith("https://hooks.zapier.com", StringComparison.OrdinalIgnoreCase))
            return HttpResults.NotFound();

        var results = await repository.GetByUrlAsync(targetUrl);
        if (results.Documents.Count > 0)
        {
            string organizationId = results.Documents.First().OrganizationId;
            if (results.Documents.Any(h => h.OrganizationId != organizationId))
                throw new ArgumentException("All OrganizationIds must be the same.");

            _logger.RemovingZapierUrls(results.Documents.Count, targetUrl);
            await repository.RemoveAsync(results.Documents);
        }

        return HttpResults.Ok();
    }

    public IResult Handle(TestWebHook message)
    {
        return HttpResults.Ok(new[] {
            new { id = 1, Message = "Test message 1." },
            new { id = 2, Message = "Test message 2." }
        });
    }

    private async Task<IResult> PostImplAsync(NewWebHook value)
    {
        if (value is null)
            return HttpResults.BadRequest();

        var mapped = mapper.MapToWebHook(value);
        if (String.IsNullOrEmpty(mapped.OrganizationId) && HttpContext.Request.GetAssociatedOrganizationIds().Count > 0)
            mapped.OrganizationId = HttpContext.Request.GetDefaultOrganizationId()!;

        var permission = await CanAddAsync(mapped);
        if (!permission.Allowed)
            return PermissionToResult(permission);

        if (!IsValidWebHookVersion(mapped.Version))
            mapped.Version = WebHook.KnownVersions.Version2;

        var model = await repository.AddAsync(mapped, o => o.Cache());
        return TypedResults.Created($"/api/v2/webhooks/{model.Id}", model);
    }

    private async Task<PermissionResult> CanAddAsync(WebHook value)
    {
        if (String.IsNullOrEmpty(value.Url) || value.EventTypes.Length == 0)
            return PermissionResult.Deny;

        if (String.IsNullOrEmpty(value.ProjectId) && String.IsNullOrEmpty(value.OrganizationId))
            return PermissionResult.Deny;

        if (!String.IsNullOrEmpty(value.OrganizationId) && !HttpContext.Request.IsInOrganization(value.OrganizationId))
            return PermissionResult.DenyWithMessage("Invalid organization id specified.");

        Project? project = null;
        if (!String.IsNullOrEmpty(value.ProjectId))
        {
            project = await GetProjectAsync(value.ProjectId);
            if (project is null)
                return PermissionResult.DenyWithMessage("Invalid project id specified.");

            value.OrganizationId = project.OrganizationId;
        }

        if (!await billingManager.HasPremiumFeaturesAsync(project is not null ? project.OrganizationId : value.OrganizationId))
            return PermissionResult.DenyWithPlanLimitReached("Please upgrade your plan to add integrations.");

        return PermissionResult.Allow;
    }

    private async Task<PermissionResult> CanDeleteAsync(WebHook value)
    {
        if (!String.IsNullOrEmpty(value.ProjectId) && !await IsInProjectAsync(value.ProjectId))
            return PermissionResult.DenyWithNotFound(value.Id);

        if (!String.IsNullOrEmpty(value.OrganizationId) && !HttpContext.Request.IsInOrganization(value.OrganizationId))
            return PermissionResult.DenyWithNotFound(value.Id);

        return PermissionResult.Allow;
    }

    private async Task<WebHook?> GetModelAsync(string id, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var webHook = await repository.GetByIdAsync(id, o => o.Cache(useCache));
        if (webHook is null)
            return null;

        if (!String.IsNullOrEmpty(webHook.OrganizationId) && !HttpContext.Request.IsInOrganization(webHook.OrganizationId))
            return null;

        if (!String.IsNullOrEmpty(webHook.ProjectId) && !await IsInProjectAsync(webHook.ProjectId))
            return null;

        return webHook;
    }

    private async Task<IReadOnlyCollection<WebHook>> GetModelsAsync(string[] ids, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var webHooks = await repository.GetByIdsAsync(ids, o => o.Cache(useCache));
        if (webHooks.Count == 0)
            return [];

        var results = new List<WebHook>();
        foreach (var webHook in webHooks)
        {
            if ((!String.IsNullOrEmpty(webHook.OrganizationId) && HttpContext.Request.IsInOrganization(webHook.OrganizationId))
                || (!String.IsNullOrEmpty(webHook.ProjectId) && await IsInProjectAsync(webHook.ProjectId)))
                results.Add(webHook);
        }

        return results;
    }

    private async Task<Project?> GetProjectAsync(string projectId, bool useCache = true)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
        if (project is null || !HttpContext.Request.CanAccessOrganization(project.OrganizationId))
            return null;

        return project;
    }

    private async Task<bool> IsInProjectAsync(string projectId)
    {
        var project = await GetProjectAsync(projectId);
        return project is not null;
    }

    private static IResult PermissionToResult(PermissionResult permission)
    {
        if (String.IsNullOrEmpty(permission.Message))
            return TypedResults.Problem(statusCode: permission.StatusCode);

        return TypedResults.Problem(statusCode: permission.StatusCode, title: permission.Message);
    }

    private static bool IsValidWebHookVersion(string version)
    {
        return String.Equals(version, WebHook.KnownVersions.Version1) || String.Equals(version, WebHook.KnownVersions.Version2);
    }

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static bool NextPageExceedsSkipLimit(int page, int limit) => (page + 1) * limit >= 1000;
}
