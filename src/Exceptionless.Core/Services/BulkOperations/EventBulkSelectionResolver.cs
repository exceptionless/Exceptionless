using Exceptionless.Core.Models;
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
/// Pages the ids of events that match an organization or project scope, a filter and an absolute UTC window, using the
/// same query building as the events list so the selection is exactly what the user was looking at.
/// </summary>
public class EventBulkSelectionResolver : IBulkSelectionResolver
{
    private const string CreatedUtcField = "created_utc";
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly PersistentEventQueryValidator _validator;
    private readonly ITextSerializer _serializer;

    public EventBulkSelectionResolver(IEventRepository eventRepository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
        PersistentEventQueryValidator validator, ITextSerializer serializer)
    {
        _eventRepository = eventRepository;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _validator = validator;
        _serializer = serializer;
    }

    public BulkOperationEntity Entity => BulkOperationEntity.Events;

    public async Task<BulkOperationCheckResult> ValidateSelectionAsync(Organization organization, BulkOperationSelection selection)
    {
        var result = await _validator.ValidateQueryAsync(selection.Filter);
        if (!result.IsValid)
            return BulkOperationCheckResult.Invalid("selection.filter", result.Message ?? "Invalid filter.");

        if (result.UsesPremiumFeatures && !organization.HasPremiumFeatures)
            return BulkOperationCheckResult.PlanLimit("Please upgrade your plan to use premium search features.");

        return BulkOperationCheckResult.Ok;
    }

    public async Task<BulkSelectionPreparation> PrepareAsync(BulkOperation operation, CancellationToken cancellationToken)
    {
        var appFilter = await GetAppFilterAsync(operation);
        if (appFilter is null)
            return new BulkSelectionPreparation(BulkOperationCheckResult.Ok);

        // The events list resolves stack level terms (for example status:open) and removes deleted stacks by looking up
        // matching stack ids. Do that once, here, and keep the result so every page uses the same stacks.
        var query = ApplyScope(new RepositoryQuery<PersistentEvent>(), appFilter, operation.Selection)
            .FilterExpression(operation.Selection.Filter)
            .EnforceEventStackFilter();

        CountResult count;
        try
        {
            count = await _eventRepository.CountAsync(query);
        }
        catch (DocumentLimitExceededException ex)
        {
            return BulkSelectionPreparation.Rejected(BulkOperationCheckResult.Invalid("selection.filter", ex.Message));
        }

        var pinned = new PinnedEventSelection
        {
            // Relative dates in the filter are pinned to the creation time, like the outer time window.
            EventFilter = await RelativeDateFreezer.FreezeAsync(query.GetFilterExpression(), operation.Selection.UtcCreatedBefore ?? operation.CreatedUtc),
            ExcludedStackIds = query.GetExcludedStacks().ToArray(),
            StackIds = query.GetStacks().ToArray()
        };

        return new BulkSelectionPreparation(BulkOperationCheckResult.Ok, count.Total, _serializer.SerializeToString(pinned));
    }

    public async Task<BulkSelectionPage> GetPageAsync(BulkOperation operation, string? pinnedState, string? cursor, int size, CancellationToken cancellationToken)
    {
        var appFilter = await GetAppFilterAsync(operation);
        if (appFilter is null || pinnedState is null)
            return new BulkSelectionPage([], null);

        var pinned = _serializer.Deserialize<PinnedEventSelection>(pinnedState) ?? new PinnedEventSelection();
        var query = ApplyScope(new RepositoryQuery<PersistentEvent>(), appFilter, operation.Selection)
            .FilterExpression(pinned.EventFilter)
            .Include(e => e.Id)
            .SortExpression($"-{EventIndex.Alias.Date}");

        if (pinned.StackIds.Length > 0)
            query.Stack(pinned.StackIds);

        if (pinned.ExcludedStackIds.Length > 0)
            query.ExcludeStack(pinned.ExcludedStackIds);

        // Paging only needs the next ids; the total was counted once when the operation was created.
        var options = new CommandOptions<PersistentEvent>().SearchAfterToken(cursor, _serializer).PageLimit(size).TrackTotalHits(false);
        var results = await _eventRepository.FindAsync(query, options);

        string[] ids = results.Hits.Select(h => h.Id).Where(id => !String.IsNullOrEmpty(id)).Select(id => id!).ToArray();
        string? nextCursor = results.Hits.LastOrDefault()?.GetSortToken(_serializer);
        return new BulkSelectionPage(ids, ids.Length > 0 ? nextCursor : null);
    }

    private static RepositoryQuery<PersistentEvent> ApplyScope(RepositoryQuery<PersistentEvent> query, AppFilter appFilter, BulkOperationSelection selection)
    {
        query.AppFilter(appFilter);

        if (selection.UtcStart.HasValue || selection.UtcEnd.HasValue)
        {
            query.DateRange(selection.UtcStart, selection.UtcEnd, EventIndex.Alias.Date)
                .Index(selection.UtcStart, selection.UtcEnd);
        }

        // The event date is chosen by the client, so an old-dated event can still arrive after the operation was created.
        if (selection.UtcCreatedBefore.HasValue)
            query.DateRange(null, selection.UtcCreatedBefore, CreatedUtcField);

        return query;
    }

    private async Task<AppFilter?> GetAppFilterAsync(BulkOperation operation)
    {
        var organization = await _organizationRepository.GetByIdAsync(operation.OrganizationId, o => o.Cache());
        if (organization is null)
            return null;

        var validation = await _validator.ValidateQueryAsync(operation.Selection.Filter);
        AppFilter appFilter;
        if (!String.IsNullOrEmpty(operation.ProjectId))
        {
            var project = await _projectRepository.GetByIdAsync(operation.ProjectId, o => o.Cache());
            if (project is null || !String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal))
                return null;

            appFilter = new AppFilter(project, organization);
        }
        else
        {
            appFilter = new AppFilter(organization);
        }

        appFilter.UsesPremiumFeatures = validation.UsesPremiumFeatures;
        return appFilter;
    }

    /// <summary>What the stack level filter resolved to when the operation was created.</summary>
    private sealed class PinnedEventSelection
    {
        public string? EventFilter { get; set; }
        public string[] ExcludedStackIds { get; set; } = [];
        public string[] StackIds { get; set; } = [];
    }
}
