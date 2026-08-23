using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Api.Handlers;

public sealed class StackRollupHandler(
    IStackRollupSearchService stackRollupSearchService,
    IStackRepository stackRepository,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    FormattingPluginManager formattingPluginManager,
    ICacheClient cacheClient,
    EventStackQueryValidator validator,
    AppOptions options,
    TimeProvider timeProvider)
{
    private static readonly ICollection<string> _allowedDateFields = ["date"];
    private const string DefaultDateField = "date";

    public async Task<Result<StackRollupStatsResult>> Handle(GetStackRollupStats message)
    {
        var organizations = await GetSelectedOrganizationsAsync(message.Context, message.Filter);
        if (organizations.All(organization => organization.IsSuspended))
            return new StackRollupStatsResult(0, 0, 0, []);

        var time = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(options.MaximumRetentionDays, timeProvider));
        return await GetStatsInternalAsync(new AppFilter(organizations) { IsUserOrganizationsFilter = true }, time, message.Filter, message.Context);
    }

    public async Task<Result<StackRollupStatsResult>> Handle(GetStackRollupStatsByOrganization message)
    {
        var organization = await GetOrganizationAsync(message.OrganizationId, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");
        if (organization.IsSuspended)
            return PlanLimitResult<StackRollupStatsResult>("Unable to view stack occurrences for the suspended organization.");

        var time = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(options.MaximumRetentionDays, timeProvider));
        return await GetStatsInternalAsync(new AppFilter(organization), time, message.Filter, message.Context);
    }

    public async Task<Result<StackRollupStatsResult>> Handle(GetStackRollupStatsByProject message)
    {
        var project = await GetProjectAsync(message.ProjectId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");
        var organization = await GetOrganizationAsync(project.OrganizationId, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");
        if (organization.IsSuspended)
            return PlanLimitResult<StackRollupStatsResult>("Unable to view stack occurrences for the suspended organization.");

        var time = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, options.MaximumRetentionDays, timeProvider));
        return await GetStatsInternalAsync(new AppFilter(project, organization), time, message.Filter, message.Context);
    }

    public async Task<Result<PagedResult<object>>> Handle(GetAllStackRollups message)
    {
        var organizations = await GetSelectedOrganizationsAsync(message.Context, message.Filter);
        if (organizations.All(organization => organization.IsSuspended))
            return new PagedResult<object>([], false);

        var time = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organizations.GetRetentionUtcCutoff(options.MaximumRetentionDays, timeProvider));
        return await GetInternalAsync(new AppFilter(organizations) { IsUserOrganizationsFilter = true }, time, message.Filter, message.Sort, message.Limit, message.Before, message.After, message.Include, message.Context);
    }

    public async Task<Result<PagedResult<object>>> Handle(GetStackRollupsByOrganization message)
    {
        var organization = await GetOrganizationAsync(message.OrganizationId, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");
        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view stack occurrences for the suspended organization.");

        var time = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(options.MaximumRetentionDays, timeProvider));
        return await GetInternalAsync(new AppFilter(organization), time, message.Filter, message.Sort, message.Limit, message.Before, message.After, message.Include, message.Context);
    }

    public async Task<Result<PagedResult<object>>> Handle(GetStackRollupsByProject message)
    {
        var project = await GetProjectAsync(message.ProjectId, message.Context);
        if (project is null)
            return Result.NotFound("Project not found.");

        var organization = await GetOrganizationAsync(project.OrganizationId, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");
        if (organization.IsSuspended)
            return PlanLimitResult<PagedResult<object>>("Unable to view stack occurrences for the suspended organization.");

        var time = TimeRangeParser.GetTimeInfo(message.Time, message.Offset, timeProvider, _allowedDateFields, DefaultDateField, organization.GetRetentionUtcCutoff(project, options.MaximumRetentionDays, timeProvider));
        return await GetInternalAsync(new AppFilter(project, organization), time, message.Filter, message.Sort, message.Limit, message.Before, message.After, message.Include, message.Context);
    }

    private async Task<Result<PagedResult<object>>> GetInternalAsync(
        AppFilter appFilter,
        TimeInfo time,
        string? filter,
        string? sort,
        int limit,
        string? before,
        string? after,
        string? include,
        HttpContext httpContext)
    {
        if (before is not null && after is not null)
            return Result.BadRequest("The before and after parameters cannot be used together.");

        limit = Pagination.GetLimit(limit);
        var validation = await validator.ValidateQueryAsync(filter);
        if (!validation.IsValid)
            return Result.BadRequest(validation.Message ?? "Invalid filter.");

        appFilter.UsesPremiumFeatures = validation.UsesPremiumFeatures;
        AppFilter? appliedAppFilter = ApiFilterPolicy.ShouldApplySystemFilter(appFilter, filter, httpContext.Request) ? appFilter : null;
        if (appliedAppFilter is not null && ApiFilterPolicy.IsPremiumFeatureQueryBlocked(appliedAppFilter))
            return PlanLimitResult<PagedResult<object>>(ApiFilterPolicy.PremiumSearchUpgradeMessage);

        try
        {
            var result = await stackRollupSearchService.SearchAsync(new StackRollupSearchRequest(
                appliedAppFilter,
                time.Range.UtcStart,
                time.Range.UtcEnd,
                time.Offset,
                httpContext.Request.Query["time"],
                filter,
                sort,
                limit,
                before,
                after,
                ShouldInclude(include, "total")), httpContext.RequestAborted);

            string[] stackIds = result.Rows.Select(row => row.StackId).ToArray();
            var stacks = (await stackRepository.GetByIdsAsync(stackIds))
                .Select(stack => stack.ApplyOffset(time.Offset))
                .ToList();
            var summaries = await GetStackSummariesAsync(stacks, result.Rows, appFilter, time);

            return new PagedResult<object>(summaries.Cast<object>().ToList(), result.HasMore, null, result.Total, result.Before, result.After);
        }
        catch (InvalidStackRollupCursorException ex)
        {
            return Result.BadRequest(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "sort")
        {
            return Result.BadRequest("Sort must be one of total, users, first_occurrence, or last_occurrence, optionally prefixed with '-'.");
        }
    }

    private async Task<Result<StackRollupStatsResult>> GetStatsInternalAsync(AppFilter appFilter, TimeInfo time, string? filter, HttpContext httpContext)
    {
        var filterValidation = await validator.ValidateQueryAsync(filter);
        if (!filterValidation.IsValid)
            return Result.BadRequest(filterValidation.Message ?? "Invalid filter.");

        appFilter.UsesPremiumFeatures = filterValidation.UsesPremiumFeatures;
        AppFilter? appliedAppFilter = ApiFilterPolicy.ShouldApplySystemFilter(appFilter, filter, httpContext.Request) ? appFilter : null;
        if (appliedAppFilter is not null && ApiFilterPolicy.IsPremiumFeatureQueryBlocked(appliedAppFilter))
            return PlanLimitResult<StackRollupStatsResult>(ApiFilterPolicy.PremiumSearchUpgradeMessage);

        return await stackRollupSearchService.GetStatsAsync(new StackRollupStatsRequest(
            appliedAppFilter,
            time.Range.UtcStart,
            time.Range.UtcEnd,
            time.Offset,
            filter), httpContext.RequestAborted);
    }

    private async Task<ICollection<StackSummaryModel>> GetStackSummariesAsync(List<Stack> stacks, IReadOnlyCollection<StackRollupRow> rows, AppFilter appFilter, TimeInfo time)
    {
        if (stacks.Count == 0)
            return [];

        var stacksById = stacks.ToDictionary(stack => stack.Id, StringComparer.Ordinal);
        var projects = await projectRepository.GetByIdsAsync(stacks.Select(stack => stack.ProjectId).Distinct().ToArray(), query => query.Cache());
        var projectNames = projects.ToDictionary(project => project.Id, project => project.Name);
        var totalUsers = await GetUserCountByProjectIdsAsync(stacks, appFilter, time.Range.UtcStart, time.Range.UtcEnd);
        var summaries = new List<StackSummaryModel>(rows.Count);

        foreach (var row in rows)
        {
            if (!stacksById.TryGetValue(row.StackId, out var stack))
                continue;

            var data = formattingPluginManager.GetStackSummaryData(stack);
            summaries.Add(new StackSummaryModel
            {
                Id = data.Id,
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                ProjectId = stack.ProjectId,
                ProjectName = projectNames.GetValueOrDefault(stack.ProjectId),
                Tags = stack.Tags?.OfType<string>().Order(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
                Title = stack.Title,
                Status = stack.Status,
                FirstOccurrence = row.FirstOccurrence,
                LastOccurrence = row.LastOccurrence,
                Total = row.Total,
                Users = row.Users,
                TotalUsers = totalUsers.GetOrDefault(stack.ProjectId)
            });
        }

        return summaries;
    }

    private async Task<Dictionary<string, double>> GetUserCountByProjectIdsAsync(ICollection<Stack> stacks, AppFilter appFilter, DateTime utcStart, DateTime utcEnd)
    {
        using var scopedCacheClient = new ScopedCacheClient(cacheClient, $"Project:user-count:{utcStart.Floor(TimeSpan.FromMinutes(15)).Ticks}-{utcEnd.Floor(TimeSpan.FromMinutes(15)).Ticks}");
        var projectIds = stacks.Select(stack => stack.ProjectId).Distinct().ToList();
        var cachedTotals = await scopedCacheClient.GetAllAsync<double>(projectIds);
        var totals = cachedTotals.Where(item => item.Value.HasValue).ToDictionary(item => item.Key, item => item.Value.Value);
        if (totals.Count == projectIds.Count)
            return totals;

        var projects = cachedTotals
            .Where(item => !item.Value.HasValue && stacks.Contains(stack => stack.ProjectId == item.Key))
            .Select(item => new Project { Id = item.Key, OrganizationId = stacks.First(stack => stack.ProjectId == item.Key).OrganizationId })
            .ToList();
        var aggregations = (await stackRollupSearchService.GetProjectUserCountsAsync(new StackRollupProjectUsersRequest(
            appFilter,
            utcStart,
            utcEnd,
            projects.Select(project => project.Id).ToArray())))
            .ToDictionary(item => item.Key, item => (double)item.Value);
        await scopedCacheClient.SetAllAsync(aggregations.Where(item => item.Value >= 10).ToDictionary(item => item.Key, item => item.Value), TimeSpan.FromMinutes(5));
        totals.AddRange(aggregations);
        return totals;
    }

    private async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(HttpContext httpContext, string? filter)
    {
        var organizationIds = httpContext.Request.GetAssociatedOrganizationIds();
        if (organizationIds.Count == 0)
            return [];

        if (!String.IsNullOrEmpty(filter))
        {
            var scope = GetFilterScopeVisitor.Run(filter);
            if (scope.IsScopable)
            {
                Organization? organization = null;
                if (scope.OrganizationId is not null)
                    organization = await organizationRepository.GetByIdAsync(scope.OrganizationId, query => query.Cache());
                else if (scope.ProjectId is not null)
                {
                    var project = await projectRepository.GetByIdAsync(scope.ProjectId, query => query.Cache());
                    if (project is not null)
                        organization = await organizationRepository.GetByIdAsync(project.OrganizationId, query => query.Cache());
                }
                else if (scope.StackId is not null)
                {
                    var stack = await stackRepository.GetByIdAsync(scope.StackId, query => query.Cache());
                    if (stack is not null)
                        organization = await organizationRepository.GetByIdAsync(stack.OrganizationId, query => query.Cache());
                }

                if (organization is not null)
                    return organizationIds.Contains(organization.Id) || httpContext.Request.IsGlobalAdmin() ? [organization] : [];
            }
        }

        return await organizationRepository.GetByIdsAsync(organizationIds.ToArray(), query => query.Cache());
    }

    private Task<Organization?> GetOrganizationAsync(string organizationId, HttpContext httpContext)
        => String.IsNullOrEmpty(organizationId) || !httpContext.Request.CanAccessOrganization(organizationId)
            ? Task.FromResult<Organization?>(null)
            : organizationRepository.GetByIdAsync(organizationId, query => query.Cache());

    private async Task<Project?> GetProjectAsync(string projectId, HttpContext httpContext)
    {
        var project = await projectRepository.GetByIdAsync(projectId, query => query.Cache());
        return project is null || !httpContext.Request.CanAccessOrganization(project.OrganizationId) ? null : project;
    }

    private static bool ShouldInclude(string? include, string value)
        => include?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(value, StringComparer.OrdinalIgnoreCase) == true;

    private static Result<T> PlanLimitResult<T>(string message)
        => Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, message));
}
