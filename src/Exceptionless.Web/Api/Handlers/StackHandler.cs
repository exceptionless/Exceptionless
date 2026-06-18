using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using McSherry.SemanticVersioning;
using PermissionResult = Exceptionless.Web.Controllers.PermissionResult;

namespace Exceptionless.Web.Api.Handlers;

public class StackHandler(
    IStackRepository stackRepository,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IEventRepository eventRepository,
    IWebHookRepository webHookRepository,
    WebHookDataPluginManager webHookDataPluginManager,
    IQueue<WebHookNotification> webHookNotificationQueue,
    ICacheClient cacheClient,
    FormattingPluginManager formattingPluginManager,
    SemanticVersionParser semanticVersionParser,
    IAppQueryValidator validator,
    AppOptions options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StackHandler>();
    private static readonly ICollection<string> _allowedDateFields = new List<string> { StackIndex.Alias.FirstOccurrence, StackIndex.Alias.LastOccurrence };
    private const string DefaultDateField = StackIndex.Alias.LastOccurrence;
    private static Result<T> PlanLimitResult<T>(string message) => Result.Invalid(ValidationError.Create("plan_limit", message));

    public async Task<Result<Stack>> Handle(GetStackById message)
    {
        var stack = await GetModelAsync(message.Id, message.Context);
        if (stack is null)
            return Result.NotFound("Stack not found.");

        var offset = TimeRangeParser.GetOffset(message.Offset);
        return stack.ApplyOffset(offset);
    }

    public async Task<Result> Handle(MarkStacksFixed message)
    {
        SemanticVersion? semanticVersion = null;

        if (!String.IsNullOrEmpty(message.Version))
        {
            semanticVersion = semanticVersionParser.Parse(message.Version);
            if (semanticVersion is null)
                return Result.BadRequest("Invalid semantic version");
        }

        var stacks = await GetModelsAsync(message.Ids.FromDelimitedString(), message.Context, false);
        if (stacks.Count is 0)
            return Result.NotFound("Stacks not found.");

        foreach (var stack in stacks)
            stack.MarkFixed(semanticVersion, timeProvider);

        await stackRepository.SaveAsync(stacks);

        return Result.Success();
    }

    public async Task<Result> Handle(MarkStacksFixedByZapier message)
    {
        string? id = null;
        if (message.Data.RootElement.TryGetProperty("ErrorStack", out var errorStackProp))
            id = errorStackProp.GetString();

        if (message.Data.RootElement.TryGetProperty("Stack", out var stackProp))
            id = stackProp.GetString();

        if (String.IsNullOrEmpty(id))
            return Result.NotFound("Stack not found.");

        if (id.StartsWith("http"))
            id = id.Substring(id.LastIndexOf('/') + 1);

        return await Handle(new MarkStacksFixed(id, null, message.Context));
    }

    public async Task<Result> Handle(SnoozeStacks message)
    {
        if (message.SnoozeUntilUtc < timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5))
            return Result.BadRequest("Must snooze for at least 5 minutes.");

        var stacks = await GetModelsAsync(message.Ids.FromDelimitedString(), message.Context, false);
        if (stacks.Count is 0)
            return Result.NotFound("Stacks not found.");

        foreach (var stack in stacks)
        {
            stack.Status = StackStatus.Snoozed;
            stack.SnoozeUntilUtc = message.SnoozeUntilUtc;
            stack.FixedInVersion = null;
            stack.DateFixed = null;
        }

        await stackRepository.SaveAsync(stacks);

        return Result.Success();
    }

    public async Task<Result> Handle(AddStackLink message)
    {
        if (String.IsNullOrWhiteSpace(message.Url?.Value))
            return Result.BadRequest("URL is required.");

        var stack = await GetModelAsync(message.Id, message.Context, false);
        if (stack is null)
            return Result.NotFound("Stack not found.");

        if (!stack.References.Contains(message.Url.Value.Trim()))
        {
            stack.References.Add(message.Url.Value.Trim());
            await stackRepository.SaveAsync(stack);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(AddStackLinkByZapier message)
    {
        string? id = null;
        if (message.Data.RootElement.TryGetProperty("ErrorStack", out var errorStackProp))
            id = errorStackProp.GetString();

        if (message.Data.RootElement.TryGetProperty("Stack", out var stackProp))
            id = stackProp.GetString();

        if (String.IsNullOrEmpty(id))
            return Result.NotFound("Stack not found.");

        if (id.StartsWith("http"))
            id = id.Substring(id.LastIndexOf('/') + 1);

        string? url = message.Data.RootElement.TryGetProperty("Link", out var linkProp) ? linkProp.GetString() : null;
        return await Handle(new AddStackLink(id, new ValueFromBody<string?>(url), message.Context));
    }

    public async Task<Result> Handle(RemoveStackLink message)
    {
        if (String.IsNullOrWhiteSpace(message.Url?.Value))
            return Result.BadRequest("URL is required.");

        var stack = await GetModelAsync(message.Id, message.Context, false);
        if (stack is null)
            return Result.NotFound("Stack not found.");

        if (stack.References.Contains(message.Url.Value.Trim()))
        {
            stack.References.Remove(message.Url.Value.Trim());
            await stackRepository.SaveAsync(stack);
        }

        return Result.NoContent();
    }

    public async Task<Result> Handle(MarkStacksCritical message)
    {
        var stacks = await GetModelsAsync(message.Ids.FromDelimitedString(), message.Context, false);
        if (stacks.Count is 0)
            return Result.NotFound("Stacks not found.");

        stacks = stacks.Where(s => !s.OccurrencesAreCritical).ToList();
        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
                stack.OccurrencesAreCritical = true;

            await stackRepository.SaveAsync(stacks);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(MarkStacksNotCritical message)
    {
        var stacks = await GetModelsAsync(message.Ids.FromDelimitedString(), message.Context, false);
        if (stacks.Count is 0)
            return Result.NotFound("Stacks not found.");

        stacks = stacks.Where(s => s.OccurrencesAreCritical).ToList();
        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
                stack.OccurrencesAreCritical = false;

            await stackRepository.SaveAsync(stacks);
        }

        return Result.NoContent();
    }

    public async Task<Result> Handle(ChangeStacksStatus message)
    {
        if (message.Status is StackStatus.Regressed or StackStatus.Snoozed)
            return Result.BadRequest("Can't set stack status to regressed or snoozed.");

        var stacks = await GetModelsAsync(message.Ids.FromDelimitedString(), message.Context, false);
        if (stacks.Count is 0)
            return Result.NotFound("Stacks not found.");

        stacks = stacks.Where(s => s.Status != message.Status).ToList();
        if (stacks.Count > 0)
        {
            foreach (var stack in stacks)
            {
                stack.Status = message.Status;
                if (message.Status == StackStatus.Fixed)
                {
                    stack.DateFixed = timeProvider.GetUtcNow().UtcDateTime;
                }
                else
                {
                    stack.DateFixed = null;
                    stack.FixedInVersion = null;
                }

                stack.SnoozeUntilUtc = null;
            }

            await stackRepository.SaveAsync(stacks);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(PromoteStack message)
    {
        var httpContext = message.Context;
        if (String.IsNullOrEmpty(message.Id))
            return Result.NotFound("Stack not found.");

        var stack = await stackRepository.GetByIdAsync(message.Id);
        if (stack is null || !httpContext.Request.CanAccessOrganization(stack.OrganizationId))
            return Result.NotFound("Stack not found.");

        var organization = await GetOrganizationAsync(stack.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (!organization.HasPremiumFeatures)
            return Result.Invalid(ValidationError.Create("plan_limit", "Promote to External is a premium feature used to promote an error stack to an external system. Please upgrade your plan to enable this feature."));

        var promotedProjectHooks = (await webHookRepository.GetByProjectIdAsync(stack.ProjectId)).Documents.Where(p => p.EventTypes.Contains(WebHook.KnownEventTypes.StackPromoted)).ToList();
        if (promotedProjectHooks.Count is 0)
            return Result.Invalid(ValidationError.Create("not_implemented", "No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature."));

        var currentUser = httpContext.Request.GetUser();
        using var _ = _logger.BeginScope(new ExceptionlessState()
            .Organization(stack.OrganizationId)
            .Project(stack.ProjectId)
            .Tag("Promote")
            .Identity(currentUser.EmailAddress)
            .Property("User", currentUser)
            .SetHttpContext(httpContext));

        var project = await GetProjectAsync(stack.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        foreach (var hook in promotedProjectHooks)
        {
            if (!hook.IsEnabled)
            {
                _logger.LogWarning("Unable to promote to disabled WebHook Id={WebHookId}, Url={WebHookUrl}", hook.Id, hook.Url);
                continue;
            }

            var context = new WebHookDataContext(hook, organization, project, stack, null, stack.TotalOccurrences == 1, stack.Status == StackStatus.Regressed);
            object? data = await webHookDataPluginManager.CreateFromStackAsync(context);
            if (data is null)
            {
                _logger.LogWarning("Unable to promote to WebHook with null payload Id={WebHookId}, Url={WebHookUrl}", hook.Id, hook.Url);
                continue;
            }

            await webHookNotificationQueue.EnqueueAsync(new WebHookNotification
            {
                OrganizationId = stack.OrganizationId,
                ProjectId = stack.ProjectId,
                WebHookId = hook.Id,
                Url = hook.Url,
                Type = WebHookType.General,
                Data = data
            });
        }

        return Result.Success();
    }

    public async Task<Result<WorkInProgressResult>> Handle(DeleteStacks message)
    {
        var httpContext = message.Context;
        var ids = message.Ids.FromDelimitedString();
        var items = await GetModelsAsync(ids, httpContext, false);
        if (items.Count == 0)
            return Result.NotFound("Stacks not found.");

        var results = new ModelActionResults();
        results.AddNotFound(ids.Except(items.Select(i => i.Id)));

        var denied = items.Where(model => model is IOwnedByOrganization orgModel && !httpContext.Request.CanAccessOrganization(orgModel.OrganizationId)).ToList();
        foreach (var model in denied)
            results.Failure.Add(PermissionResult.DenyWithNotFound(model.Id));

        var list = items.Except(denied).ToList();

        if (list.Count == 0)
            return results.Failure.Count == 1 ? PermissionToResult(results.Failure.First()) : results;

        var currentUser = httpContext.Request.GetUser();
        foreach (var projectStacks in list.GroupBy(ev => ev.ProjectId))
        {
            var stack = projectStacks.First();
            using var _ = _logger.BeginScope(new ExceptionlessState().Organization(stack.OrganizationId).Project(stack.ProjectId).Tag("Delete").Identity(currentUser.EmailAddress).Property("User", currentUser).SetHttpContext(httpContext));
            _logger.LogInformation("User {User} deleted {RemovedCount} stacks in project ({ProjectId})", currentUser.Id, projectStacks.Count(), stack.ProjectId);
        }

        list.ForEach(v => v.IsDeleted = true);
        await stackRepository.SaveAsync(list);

        if (results.Failure.Count == 0)
            return new WorkInProgressResult();

        results.Success.AddRange(list.Select(i => i.Id));
        return results;
    }

    public async Task<Result<PagedResult<object>>> Handle(GetAllStacks message)
    {
        var httpContext = message.Context;
        var organizations = await GetSelectedOrganizationsAsync(httpContext, message.Filter);
        if (organizations.All(o => o.IsSuspended))
            return new PagedResult<object>(Array.Empty<object>(), false);

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(options.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organizations) { IsUserOrganizationsFilter = true };
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit);
    }

    public async Task<Result<PagedResult<object>>> Handle(GetStacksByOrganization message)
    {
        var httpContext = message.Context;
        var organization = await GetOrganizationAsync(message.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view stack occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(options.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(organization);
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit);
    }

    public async Task<Result<PagedResult<object>>> Handle(GetStacksByProject message)
    {
        var httpContext = message.Context;
        var project = await GetProjectAsync(message.ProjectId, httpContext);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, httpContext);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view stack occurrences for the suspended organization.");

        var ti = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, options.MaximumRetentionDays, timeProvider));
        var sf = new AppFilter(project, organization);
        return await GetInternalAsync(sf, ti, httpContext, message.Filter, message.Sort, message.Mode, message.Page, message.Limit);
    }

    private async Task<Result<PagedResult<object>>> GetInternalAsync(AppFilter sf, TimeInfo ti, HttpContext httpContext, string? filter = null, string? sort = null, string? mode = null, int page = 1, int limit = 10)
    {
        page = Pagination.GetPage(page);
        limit = Pagination.GetLimit(limit);
        int skip = Pagination.GetSkip(page, limit);
        if (skip > Pagination.MaximumSkip)
            return new PagedResult<object>(Array.Empty<object>(), false);

        var pr = await validator.ValidateQueryAsync(filter);
        if (!pr.IsValid)
            return Result.BadRequest(pr.Message ?? "Invalid filter.");

        sf.UsesPremiumFeatures = pr.UsesPremiumFeatures;

        try
        {
            var results = await stackRepository.FindAsync(q => q.AppFilter(ShouldApplySystemFilter(sf, filter, httpContext.Request) ? sf : null).FilterExpression(filter).SortExpression(sort).DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, ti.Field), o => o.PageNumber(page).PageLimit(limit));

            var stacks = results.Documents.Select(s => s.ApplyOffset(ti.Offset)).ToList();
            if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase))
                return new PagedResult<object>((await GetStackSummariesAsync(stacks, sf, ti)).Cast<object>().ToList(), results.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page);

            return new PagedResult<object>(stacks.Cast<object>().ToList(), results.HasMore && !Pagination.NextPageExceedsSkipLimit(page, limit), page);
        }
        catch (ApplicationException ex)
        {
            var currentUser = httpContext.Request.GetUser();
            using (_logger.BeginScope(new ExceptionlessState().Property("Search Filter", new { SystemFilter = sf, UserFilter = filter, Time = ti, Page = page, Limit = limit }).Tag("Search").Identity(currentUser?.EmailAddress).Property("User", currentUser).SetHttpContext(httpContext)))
                _logger.LogError(ex, "An error has occurred. Please check your search filter");

            throw;
        }
    }

    private static bool ShouldApplySystemFilter(AppFilter sf, string? filter, HttpRequest? request = null)
    {
        // Apply filter to non admin users.
        if (request is null || !request.IsGlobalAdmin())
            return true;

        // Apply filter as it's scoped via a controller action.
        if (!sf.IsUserOrganizationsFilter)
            return true;

        // Empty user filter
        if (String.IsNullOrEmpty(filter))
            return true;

        // Used for impersonating a user. Only skip the filter if it contains an org, project or stack.
        var scope = GetFilterScopeVisitor.Run(filter);
        return !scope.HasScope;
    }

    private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, AppFilter eventSystemFilter, TimeInfo ti)
    {
        if (stacks.Count == 0)
            return new List<StackSummaryModel>();

        var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(eventSystemFilter).DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, (PersistentEvent e) => e.Date).Index(ti.Range.UtcStart, ti.Range.UtcEnd);
        var stackTerms = await eventRepository.CountAsync(q => q.SystemFilter(systemFilter).Stack(stacks.Select(r => r.Id)).AggregationsExpression($"terms:(stack_id~{stacks.Count} cardinality:user sum:count~1 min:date max:date)"));
        var buckets = stackTerms.Aggregations.Terms<string>("terms_stack_id")?.Buckets ?? [];
        return await GetStackSummariesAsync(stacks, buckets, eventSystemFilter, ti);
    }

    private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(ICollection<Stack> stacks, IReadOnlyCollection<KeyedBucket<string>> stackTerms, AppFilter sf, TimeInfo ti)
    {
        if (stacks.Count == 0)
            return new List<StackSummaryModel>(0);

        var totalUsers = await GetUserCountByProjectIdsAsync(stacks, sf, ti.Range.UtcStart, ti.Range.UtcEnd);
        return stacks.Join(stackTerms, s => s.Id, tk => tk.Key, (stack, term) =>
        {
            var data = formattingPluginManager.GetStackSummaryData(stack);
            var summary = new StackSummaryModel
            {
                Id = data.Id,
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                Title = stack.Title,
                Status = stack.Status,
                FirstOccurrence = term.Aggregations.Min<DateTime>("min_date")?.Value ?? stack.FirstOccurrence,
                LastOccurrence = term.Aggregations.Max<DateTime>("max_date")?.Value ?? stack.LastOccurrence,
                Total = (long)(term.Aggregations.Sum("sum_count")?.Value ?? term.Total.GetValueOrDefault()),

                Users = term.Aggregations.Cardinality("cardinality_user")?.Value.GetValueOrDefault() ?? 0,
                TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
            };

            return summary;
        }).ToList();
    }

    private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, AppFilter sf, DateTime utcStart, DateTime utcEnd)
    {
        using var scopedCacheClient = new ScopedCacheClient(cacheClient, $"Project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
        var projectIds = stacks.Select(s => s.ProjectId).Distinct().ToList();
        var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);

        var totals = cachedTotals.Where(kvp => kvp.Value.HasValue).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
        if (totals.Count == projectIds.Count)
            return totals;

        var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(sf).DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date).Index(utcStart, utcEnd);
        var projects = cachedTotals
            .Where(kvp => !kvp.Value.HasValue && stacks.Contains(s => s.ProjectId == kvp.Key))
            .Select(kvp => new Project { Id = kvp.Key, OrganizationId = stacks.First(s => s.ProjectId == kvp.Key).OrganizationId })
            .ToList();
        var countResult = await eventRepository.CountAsync(q => q.SystemFilter(systemFilter).FilterExpression(projects.BuildFilter()).AggregationsExpression("terms:(project_id cardinality:user)"));

        var projectTerms = countResult.Aggregations.Terms<string>("terms_project_id")?.Buckets ?? [];
        var aggregations = projectTerms.ToDictionary(t => t.Key, t => t.Aggregations.Cardinality("cardinality_user")?.Value.GetValueOrDefault() ?? 0);
        await scopedCacheClient.SetAllAsync(aggregations.Where(t => t.Value >= 10).ToDictionary(k => k.Key, v => v.Value), TimeSpan.FromMinutes(5));
        totals.AddRange(aggregations);

        return totals;
    }

    private async Task<Stack?> GetModelAsync(string id, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var model = await stackRepository.GetByIdAsync(id, o => o.Cache(useCache));
        if (model is null)
            return null;

        if (!httpContext.Request.CanAccessOrganization(model.OrganizationId))
            return null;

        return model;
    }

    private async Task<IList<Stack>> GetModelsAsync(string[] ids, HttpContext httpContext, bool useCache = true)
    {
        if (ids.Length == 0)
            return [];

        var models = await stackRepository.GetByIdsAsync(ids, o => o.Cache(useCache));
        return models.Where(m => httpContext.Request.CanAccessOrganization(m.OrganizationId)).ToList();
    }

    private Task<Organization?> GetOrganizationAsync(string? organizationId, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(organizationId) || !httpContext.Request.CanAccessOrganization(organizationId))
            return Task.FromResult<Organization?>(null);

        return organizationRepository.GetByIdAsync(organizationId, o => o.Cache(useCache));
    }

    private async Task<Project?> GetProjectAsync(string? projectId, HttpContext httpContext, bool useCache = true)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache(useCache));
        if (project is null || !httpContext.Request.CanAccessOrganization(project.OrganizationId))
            return null;

        return project;
    }

    private async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(HttpContext httpContext, string? filter = null)
    {
        var associatedOrganizationIds = httpContext.Request.GetAssociatedOrganizationIds();
        if (associatedOrganizationIds.Count == 0)
            return Array.Empty<Organization>();

        if (!String.IsNullOrEmpty(filter))
        {
            var scope = GetFilterScopeVisitor.Run(filter);
            if (scope.IsScopable)
            {
                Organization? organization = null;
                if (scope.OrganizationId is not null)
                {
                    organization = await organizationRepository.GetByIdAsync(scope.OrganizationId, o => o.Cache());
                }
                else if (scope.ProjectId is not null)
                {
                    var project = await projectRepository.GetByIdAsync(scope.ProjectId, o => o.Cache());
                    if (project is not null)
                        organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
                }
                else if (scope.StackId is not null)
                {
                    var stack = await stackRepository.GetByIdAsync(scope.StackId, o => o.Cache());
                    if (stack is not null)
                        organization = await organizationRepository.GetByIdAsync(stack.OrganizationId, o => o.Cache());
                }

                if (organization is not null)
                {
                    if (associatedOrganizationIds.Contains(organization.Id) || httpContext.Request.IsGlobalAdmin())
                        return new[] { organization };

                    return Array.Empty<Organization>();
                }
            }
        }

        return await organizationRepository.GetByIdsAsync(associatedOrganizationIds.ToArray(), o => o.Cache());
    }

    private static Result<WorkInProgressResult> PermissionToResult(PermissionResult permission)
    {
        if (!String.IsNullOrEmpty(permission.Message))
            return Result.NotFound(permission.Message);

        return Result.NotFound("Access denied.");
    }
}
