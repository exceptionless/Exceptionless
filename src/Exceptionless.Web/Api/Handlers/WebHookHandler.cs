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
using Foundatio.Mediator;
using Foundatio.Repositories;

namespace Exceptionless.Web.Api.Handlers;

public class WebHookHandler(
    IWebHookRepository repository,
    IProjectRepository projectRepository,
    BillingManager billingManager,
    ApiMapper mapper,
    LinkGenerator linkGenerator,
    IHttpContextAccessor httpContextAccessor,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<WebHookHandler>();
    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result<PagedResult<Exceptionless.Core.Models.WebHook>>> Handle(GetWebHooksByProject message)
    {
        var project = await GetProjectAsync(message.ProjectId);
        if (project is null)
            return Result.NotFound("Project not found.");

        int page = GetPage(message.Page);
        int limit = GetLimit(message.Limit);
        var results = await repository.GetByProjectIdAsync(message.ProjectId, o => o.PageNumber(page).PageLimit(limit));
        return new PagedResult<Exceptionless.Core.Models.WebHook>(results.Documents.ToArray(), results.HasMore && !NextPageExceedsSkipLimit(page, limit), page, results.Total);
    }

    public async Task<Result<Exceptionless.Core.Models.WebHook>> Handle(GetWebHookById message)
    {
        var model = await GetModelAsync(message.Id);
        return model is null ? Result.NotFound("Web hook not found.") : model;
    }

    public Task<Result<Exceptionless.Core.Models.WebHook>> Handle(CreateWebHook message) => PostImplAsync(message.WebHook);

    public async Task<Result<ModelActionResults>> Handle(DeleteWebHooks message)
    {
        var items = await GetModelsAsync(message.Ids, useCache: false);
        if (items.Count == 0)
            return Result.NotFound("No web hooks found.");

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
        {
            if (results.Failure.Count == 1)
                return Result<ModelActionResults>.FromResult(PermissionToResult(results.Failure.First()));

            return results;
        }

        await repository.RemoveAsync(deletableItems);

        if (results.Failure.Count == 0)
            return new ModelActionResults();

        results.Success.AddRange(deletableItems.Select(i => i.Id));
        return results;
    }

    public async Task<Result<Exceptionless.Core.Models.WebHook>> Handle(SubscribeWebHook message)
    {
        string? eventType = message.Data.RootElement.TryGetProperty("event", out var eventProp) ? eventProp.GetString() : null;
        string? url = message.Data.RootElement.TryGetProperty("target_url", out var urlProp) ? urlProp.GetString() : null;
        if (String.IsNullOrEmpty(eventType) || String.IsNullOrEmpty(url))
            return Result.BadRequest("Webhook subscription event and target_url are required.");

        string? projectId = HttpContext.User.GetProjectId();
        if (projectId is null)
            return Result.BadRequest("Project id is required.");

        string? organizationId = HttpContext.Request.GetDefaultOrganizationId();
        if (organizationId is null)
            return Result.BadRequest("Organization id is required.");

        var webHook = new NewWebHook
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            EventTypes = [eventType],
            Url = url,
            Version = new Version(message.ApiVersion >= 0 ? message.ApiVersion : 0, 0)
        };

        if (!webHook.Url.StartsWith("https://hooks.zapier.com"))
            return Result.NotFound("Webhook target not found.");

        return await PostImplAsync(webHook);
    }

    public async Task<Result> Handle(UnsubscribeWebHook message)
    {
        string? targetUrl = message.Data.RootElement.TryGetProperty("target_url", out var urlProp) ? urlProp.GetString() : null;
        if (targetUrl is null || !targetUrl.StartsWith("https://hooks.zapier.com"))
            return Result.NotFound("Webhook target not found.");

        var results = await repository.GetByUrlAsync(targetUrl);
        if (results.Documents.Count > 0)
        {
            string organizationId = results.Documents.First().OrganizationId;
            if (results.Documents.Any(h => h.OrganizationId != organizationId))
                throw new ArgumentException("All OrganizationIds must be the same.");

            _logger.RemovingZapierUrls(results.Documents.Count, targetUrl);
            await repository.RemoveAsync(results.Documents);
        }

        return Result.Success();
    }

    public Result<object[]> Handle(TestWebHook message)
    {
        return new object[] {
            new { id = 1, Message = "Test message 1." },
            new { id = 2, Message = "Test message 2." }
        };
    }

    private async Task<Result<Exceptionless.Core.Models.WebHook>> PostImplAsync(NewWebHook value)
    {
        if (value is null)
            return Result.BadRequest("Web hook value is required.");

        var mapped = mapper.MapToWebHook(value);
        if (String.IsNullOrEmpty(mapped.OrganizationId) && HttpContext.Request.GetAssociatedOrganizationIds().Count > 0)
            mapped.OrganizationId = HttpContext.Request.GetDefaultOrganizationId()!;

        var error = await CanAddAsync(mapped);
        if (error is not null)
            return error;

        if (!IsValidWebHookVersion(mapped.Version))
            mapped.Version = WebHook.KnownVersions.Version2;

        var model = await repository.AddAsync(mapped, o => o.Cache());
        string location = linkGenerator.GetUriByName(HttpContext, "GetWebHookById", new { id = model.Id })
            ?? throw new InvalidOperationException("Unable to generate web hook location.");
        return Result<Exceptionless.Core.Models.WebHook>.Created(model, location);
    }

    private async Task<Result<Exceptionless.Core.Models.WebHook>?> CanAddAsync(Exceptionless.Core.Models.WebHook value)
    {
        if (String.IsNullOrEmpty(value.Url) || value.EventTypes is null || value.EventTypes.Length == 0)
            return Result.BadRequest("Url and EventTypes are required.");

        if (String.IsNullOrEmpty(value.ProjectId) && String.IsNullOrEmpty(value.OrganizationId))
            return Result.Forbidden("Access denied.");

        if (!String.IsNullOrEmpty(value.OrganizationId) && !HttpContext.Request.IsInOrganization(value.OrganizationId))
            return Result.Invalid(ValidationError.Create("organization_id", "Invalid organization id specified."));

        Project? project = null;
        if (!String.IsNullOrEmpty(value.ProjectId))
        {
            project = await GetProjectAsync(value.ProjectId);
            if (project is null)
                return Result.Invalid(ValidationError.Create("project_id", "Invalid project id specified."));

            value.OrganizationId = project.OrganizationId;
        }

        if (!await billingManager.HasPremiumFeaturesAsync(project is not null ? project.OrganizationId : value.OrganizationId))
            return Result.Invalid(ValidationError.Create("plan_limit", "Please upgrade your plan to add integrations."));

        return null;
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

        var organizationMatches = webHooks
            .Where(webHook => !String.IsNullOrEmpty(webHook.OrganizationId) && HttpContext.Request.IsInOrganization(webHook.OrganizationId))
            .ToList();

        var projectAccessChecks = webHooks
            .Where(webHook => !organizationMatches.Contains(webHook) && !String.IsNullOrEmpty(webHook.ProjectId))
            .Select(async webHook => new
            {
                WebHook = webHook,
                HasAccess = await IsInProjectAsync(webHook.ProjectId!)
            });

        var projectMatches = (await Task.WhenAll(projectAccessChecks))
            .Where(result => result.HasAccess)
            .Select(result => result.WebHook);

        return organizationMatches.Concat(projectMatches).ToList();
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

    private static Result PermissionToResult(PermissionResult permission)
    {
        if (permission.StatusCode is StatusCodes.Status404NotFound)
            return Result.NotFound(permission.Message ?? "Not found.");

        return Result.Forbidden(permission.Message ?? "Access denied.");
    }

    private static bool IsValidWebHookVersion(string version)
    {
        return String.Equals(version, WebHook.KnownVersions.Version1) || String.Equals(version, WebHook.KnownVersions.Version2);
    }

    private static int GetPage(int page) => page < 1 ? 1 : page;
    private static int GetLimit(int limit) => limit < 1 ? 10 : limit > 100 ? 100 : limit;
    private static bool NextPageExceedsSkipLimit(int page, int limit) => (page + 1) * limit >= 1000;
}
