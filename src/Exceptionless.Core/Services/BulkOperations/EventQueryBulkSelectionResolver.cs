using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Exceptionless.Core.Repositories.Base;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Serializer;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// What the events based resolvers share: an organization or project scope, a filter and an absolute UTC window, using the
/// same query building as the events and stacks lists so the selection is exactly what the user was looking at. Counting
/// and paging differ per entity.
/// </summary>
public abstract class EventQueryBulkSelectionResolver : IBulkSelectionResolver
{
    private const string PremiumSearchUpgradeMessage = "Please upgrade your plan to use premium search features.";
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IAppQueryValidator _validator;
    private readonly ITextSerializer _serializer;
    private readonly ExceptionlessElasticConfiguration _configuration;

    protected EventQueryBulkSelectionResolver(IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
        IAppQueryValidator validator, ITextSerializer serializer, ExceptionlessElasticConfiguration configuration)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _validator = validator;
        _serializer = serializer;
        _configuration = configuration;
    }

    public abstract BulkOperationEntity Entity { get; }

    public async Task<BulkOperationCheckResult> ValidateSelectionAsync(Organization organization, BulkOperationSelection selection)
    {
        var result = await _validator.ValidateQueryAsync(selection.Filter);
        if (!result.IsValid)
            return BulkOperationCheckResult.Invalid("selection.filter", result.Message ?? "Invalid filter.");

        if (result.UsesPremiumFeatures && !organization.HasPremiumFeatures)
            return BulkOperationCheckResult.PlanLimit(PremiumSearchUpgradeMessage);

        return BulkOperationCheckResult.Ok;
    }

    public async Task<BulkSelectionPreparation> PrepareAsync(BackgroundTask task, CancellationToken cancellationToken)
    {
        var (appFilter, check) = await GetAppFilterAsync(task);
        if (!check.IsOk)
            return BulkSelectionPreparation.Rejected(check);

        if (appFilter is null)
            return new BulkSelectionPreparation(BulkOperationCheckResult.Ok);

        // Relative dates are pinned to the creation time, like the outer time window. Freeze before counting so the count
        // and every page share one anchor, even when preparation runs after the selected day.
        string? frozenFilter = await RelativeDateFreezer.FreezeAsync(task.Bulk!.Selection.Filter, task.Bulk.Selection.UtcCreatedBefore ?? task.CreatedUtc, IsDateFieldAsync);

        // The events list resolves stack level terms (for example status:open) and removes deleted stacks by looking up
        // matching stack ids. Do that once, here, and keep the result so every page uses the same stacks.
        var query = ApplyScope(new RepositoryQuery<PersistentEvent>(), appFilter, task.Bulk!.Selection)
            .FilterExpression(frozenFilter)
            .EnforceEventStackFilter();

        long total;
        try
        {
            total = await CountAsync(query);
        }
        catch (DocumentLimitExceededException ex)
        {
            return BulkSelectionPreparation.Rejected(BulkOperationCheckResult.Invalid("selection.filter", ex.Message));
        }

        var pinned = new PinnedEventSelection
        {
            EventFilter = query.GetFilterExpression(),
            ExcludedStackIds = query.GetExcludedStacks().ToArray(),
            StackIds = query.GetStacks().ToArray()
        };

        return new BulkSelectionPreparation(BulkOperationCheckResult.Ok, total, _serializer.SerializeToString(pinned));
    }

    public async Task<BulkSelectionPage> GetPageAsync(BackgroundTask task, string? pinnedState, string? cursor, int size, CancellationToken cancellationToken)
    {
        var (appFilter, check) = await GetAppFilterAsync(task);
        if (!check.IsOk)
        {
            throw new BulkSelectionRejectedException(check);
        }

        if (appFilter is null || pinnedState is null)
            return new BulkSelectionPage([], null);

        var pinned = _serializer.Deserialize<PinnedEventSelection>(pinnedState) ?? new PinnedEventSelection();
        var query = ApplyScope(new RepositoryQuery<PersistentEvent>(), appFilter, task.Bulk!.Selection)
            .FilterExpression(pinned.EventFilter);

        if (pinned.StackIds.Length > 0)
        {
            query.Stack(pinned.StackIds);
        }

        if (pinned.ExcludedStackIds.Length > 0)
        {
            query.ExcludeStack(pinned.ExcludedStackIds);
        }

        // Paging only needs the next ids; the total was counted once when the task was created.
        return await FindPageAsync(query, cursor, size);
    }

    /// <summary>Counts what the query matches, in the unit of the entity. Runs once, when the task is created.</summary>
    protected abstract Task<long> CountAsync(RepositoryQuery<PersistentEvent> query);

    /// <summary>Returns the next page of ids after <paramref name="cursor"/> for a query that already carries the pinned selection.</summary>
    protected abstract Task<BulkSelectionPage> FindPageAsync(RepositoryQuery<PersistentEvent> query, string? cursor, int size);

    protected ITextSerializer Serializer => _serializer;

    private ValueTask<bool> IsDateFieldAsync(string field)
    {
        // Event searches also support stack dates, so fall back to the stacks mapping.
        var property = _configuration.Events.MappingResolver.GetMappingProperty(field, followAlias: true)
            ?? _configuration.Stacks.MappingResolver.GetMappingProperty(field, followAlias: true);
        return ValueTask.FromResult(property is DateProperty);
    }

    private static RepositoryQuery<PersistentEvent> ApplyScope(RepositoryQuery<PersistentEvent> query, AppFilter appFilter, BulkOperationSelection selection)
    {
        query.AppFilter(appFilter);

        if (selection.UtcStart.HasValue || selection.UtcEnd.HasValue)
        {
            query.DateRange(selection.UtcStart, selection.UtcEnd, EventIndex.Alias.Date)
                .Index(selection.UtcStart, selection.UtcEnd);
        }

        // The event date is chosen by the client, so an old-dated event can still arrive after the task was created.
        if (selection.UtcCreatedBefore.HasValue)
        {
            query.DateRange(null, selection.UtcCreatedBefore, (PersistentEvent e) => e.CreatedUtc);
        }

        return query;
    }

    private async Task<(AppFilter? Filter, BulkOperationCheckResult Check)> GetAppFilterAsync(BackgroundTask task)
    {
        var organization = await _organizationRepository.GetByIdAsync(task.OrganizationId, o => o.Cache());
        if (organization is null)
            return (null, BulkOperationCheckResult.Ok);

        var validation = await _validator.ValidateQueryAsync(task.Bulk!.Selection.Filter);
        AppFilter appFilter;
        if (!String.IsNullOrEmpty(task.ProjectId))
        {
            var project = await _projectRepository.GetByIdAsync(task.ProjectId, o => o.Cache());
            if (project is null || !String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal))
                return (null, BulkOperationCheckResult.Ok);

            appFilter = new AppFilter(project, organization);
        }
        else
        {
            appFilter = new AppFilter(organization);
        }

        // AppFilter turns an unavailable premium search into no matches. Report the plan change explicitly so the worker
        // cannot mistake it for an exhausted selection and record the deletion as completed.
        if (validation.UsesPremiumFeatures && !organization.HasPremiumFeatures)
            return (null, BulkOperationCheckResult.PlanLimit(PremiumSearchUpgradeMessage));

        appFilter.UsesPremiumFeatures = validation.UsesPremiumFeatures;
        return (appFilter, BulkOperationCheckResult.Ok);
    }

    /// <summary>What the stack level filter resolved to when the task was created.</summary>
    private sealed record PinnedEventSelection
    {
        public string? EventFilter { get; init; }
        public string[] ExcludedStackIds { get; init; } = [];
        public string[] StackIds { get; init; } = [];
    }
}
