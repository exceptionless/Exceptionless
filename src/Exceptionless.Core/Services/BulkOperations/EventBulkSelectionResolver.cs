using System.Globalization;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// Pages the ids of events that match an organization or project scope, a filter and an absolute UTC window, using the
/// same query building as the events list so the selection is exactly what the user was looking at.
/// </summary>
public class EventBulkSelectionResolver : IBulkSelectionResolver
{
    private const string CreatedUtcField = "created_utc";
    private const string PremiumSearchUpgradeMessage = "Please upgrade your plan to use premium search features.";
    private const string SnapshotUnavailableMessage = "The saved event selection expired or is unavailable. Please start the operation again.";
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly PersistentEventQueryValidator _validator;
    private readonly ITextSerializer _serializer;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly AppOptions _appOptions;
    private readonly BulkOperationOptions _options;
    private readonly ILogger _logger;

    public EventBulkSelectionResolver(IEventRepository eventRepository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
        PersistentEventQueryValidator validator, ITextSerializer serializer, ExceptionlessElasticConfiguration configuration, AppOptions appOptions, BulkOperationOptions options)
    {
        _eventRepository = eventRepository;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _validator = validator;
        _serializer = serializer;
        _configuration = configuration;
        _appOptions = appOptions;
        _options = options;
        _logger = configuration.LoggerFactory.CreateLogger<EventBulkSelectionResolver>();
    }

    private ValueTask<bool> IsDateFieldAsync(string field)
    {
        return ValueTask.FromResult(_configuration.Events.MappingResolver.GetMappingProperty(field, followAlias: true) is DateProperty);
    }

    public BulkOperationEntity Entity => BulkOperationEntity.Events;

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
            return new BulkSelectionPreparation(BulkOperationCheckResult.Ok, 0,
                _serializer.SerializeToString(new PinnedEventSelection { Version = 1, IsEmpty = true }));

        // Counting and paging must use the same relative-date anchor, even when preparation runs after the selected day.
        string? frozenFilter = await RelativeDateFreezer.FreezeAsync(task.Bulk!.Selection.Filter,
            task.Bulk.Selection.UtcCreatedBefore ?? task.CreatedUtc, IsDateFieldAsync);

        var pinned = CreatePinnedSelection(appFilter);
        // Resolve stack terms once and open an event snapshot in the same search that counts the selection. A created-at
        // cutoff alone cannot pin mutable event fields such as the value of an active session or its user description.
        var query = ApplyScope(new RepositoryQuery<PersistentEvent>(), appFilter, task.Bulk!.Selection, pinned)
            .FilterExpression(frozenFilter)
            .Include(e => e.Id)
            .SortExpression($"-{EventIndex.Alias.Date}")
            .EnforceEventStackFilter();

        string? pointInTimeId = null;
        bool retainSnapshot = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var results = await _eventRepository.FindAsync(query, SnapshotOptions(Math.Max(1, _options.BatchSize)).TrackTotalHits());
            pointInTimeId = results.GetPointInTimeId();
            cancellationToken.ThrowIfCancellationRequested();
            if (String.IsNullOrEmpty(pointInTimeId))
                return BulkSelectionPreparation.Rejected(SnapshotUnavailable());

            pinned.EventFilter = query.GetFilterExpression();
            pinned.ExcludedStackIds = query.GetExcludedStacks().ToArray();
            pinned.StackIds = query.GetStacks().ToArray();
            pinned.Total = results.Total;
            pinned.IsEmpty = results.Total == 0;

            string[] ids = GetIds(results);
            if (results.Total <= ids.Length)
            {
                // The repository may already have closed a snapshot that fits on one page. Keep its complete, bounded
                // membership instead of retaining a server resource for a small operation.
                pinned.Ids = ids;
            }
            else
            {
                pinned.PointInTimeId = pointInTimeId;
            }

            string state = _serializer.SerializeToString(pinned);
            retainSnapshot = pinned.PointInTimeId is not null;
            return new BulkSelectionPreparation(BulkOperationCheckResult.Ok, results.Total, state);
        }
        catch (DocumentLimitExceededException ex)
        {
            return BulkSelectionPreparation.Rejected(BulkOperationCheckResult.Invalid("selection.filter", ex.Message));
        }
        finally
        {
            if (!retainSnapshot)
                await CloseSnapshotAsync(pointInTimeId, task.Id);
        }
    }

    public async Task<BulkSelectionPage> GetPageAsync(BackgroundTask task, string? pinnedState, string? cursor, int size, CancellationToken cancellationToken)
    {
        var pinned = ReadPinnedSelection(pinnedState);
        var position = String.IsNullOrEmpty(cursor) ? new SelectionCursor() : _serializer.Deserialize<SelectionCursor>(cursor)
            ?? throw new BulkSelectionRejectedException(SnapshotUnavailable());
        if (position.Position < 0)
            throw new BulkSelectionRejectedException(SnapshotUnavailable());

        // A final checkpoint can survive a host restart before completion is recorded. There is no remaining action to
        // authorize in that case, and a later plan change must not turn already completed work into a failed task.
        if (position.Exhausted || pinned.IsEmpty || pinned.Ids is { Length: 0 })
            return new BulkSelectionPage([], null);

        var (appFilter, check) = await GetAppFilterAsync(task);
        if (!check.IsOk)
            throw new BulkSelectionRejectedException(check);

        if (appFilter is null)
            return new BulkSelectionPage([], null);

        cancellationToken.ThrowIfCancellationRequested();
        size = Math.Max(1, size);
        var query = ApplyScope(new RepositoryQuery<PersistentEvent>(), appFilter, task.Bulk!.Selection, pinned).Include(e => e.Id);

        if (pinned.Ids is not null)
        {
            // Recheck current access policy, but never re-evaluate mutable user predicates. Search every remaining
            // bounded id so a missing or newly excluded prefix does not hide eligible ids on a later page.
            string[] remaining = pinned.Ids.Skip((int)Math.Min(position.Position, pinned.Ids.Length)).ToArray();
            if (remaining.Length == 0)
                return new BulkSelectionPage([], null);

            var available = await _eventRepository.FindAsync(query.Id(remaining), new CommandOptions<PersistentEvent>().PageLimit(remaining.Length).TrackTotalHits(false));
            var allowed = GetIds(available).ToHashSet(StringComparer.Ordinal);
            string[] ids = remaining.Where(allowed.Contains).Take(size).ToArray();
            int next = ids.Length == 0 ? pinned.Ids.Length : Array.IndexOf(pinned.Ids, ids[^1]) + 1;
            return new BulkSelectionPage(ids, _serializer.SerializeToString(new SelectionCursor { Position = next, Exhausted = next >= pinned.Ids.Length }));
        }

        query.FilterExpression(pinned.EventFilter).SortExpression($"-{EventIndex.Alias.Date}");

        if (pinned.StackIds.Length > 0)
            query.Stack(pinned.StackIds);

        if (pinned.ExcludedStackIds.Length > 0)
            query.ExcludeStack(pinned.ExcludedStackIds);

        // Always supply the saved snapshot id. Missing or expired state must never open a new live selection.
        string pointInTimeId = position.PointInTimeId ?? pinned.PointInTimeId!;
        if (String.IsNullOrEmpty(pointInTimeId) || (position.Position > 0 && String.IsNullOrEmpty(position.After)))
            throw new BulkSelectionRejectedException(SnapshotUnavailable());

        var options = SnapshotOptions(size).PointInTimeId(pointInTimeId).SearchAfterToken(position.After, _serializer).TrackTotalHits(false);
        var results = await _eventRepository.FindAsync(query, options);
        // Foundatio represents a missing PIT (404) as an empty result. A valid PIT response includes its id even when
        // current retention policy has narrowed the selection to no matches.
        string? nextPointInTimeId = results.GetPointInTimeId();
        if (String.IsNullOrEmpty(nextPointInTimeId))
            throw new BulkSelectionRejectedException(SnapshotUnavailable());

        string[] pageIds = GetIds(results);
        long nextPosition = position.Position + pageIds.Length;
        string? after = results.Hits.LastOrDefault()?.GetSortToken(_serializer);
        bool exhausted = !results.HasMore || nextPosition >= pinned.Total;
        if (!exhausted && String.IsNullOrEmpty(after))
            throw new BulkSelectionRejectedException(SnapshotUnavailable());

        // Retain the latest PIT id even on the last page, so terminal cleanup and a restart after the final checkpoint
        // can close the correct resource without replaying the selection.
        return new BulkSelectionPage(pageIds, _serializer.SerializeToString(new SelectionCursor
        {
            After = after,
            PointInTimeId = nextPointInTimeId,
            Position = nextPosition,
            Exhausted = exhausted
        }));
    }

    public async Task ReleaseAsync(BackgroundTask task, string? pinnedState, string? cursor)
    {
        if (pinnedState is null)
            return;

        try
        {
            var pinned = _serializer.Deserialize<PinnedEventSelection>(pinnedState);
            if (String.IsNullOrEmpty(pinned?.PointInTimeId))
                return;

            var position = String.IsNullOrEmpty(cursor) ? null : _serializer.Deserialize<SelectionCursor>(cursor);
            await CloseSnapshotAsync(position?.PointInTimeId ?? pinned.PointInTimeId, task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not release the event selection for task {TaskId}: {Message}", task.Id, ex.Message);
        }
    }

    private async Task CloseSnapshotAsync(string? pointInTimeId, string taskId)
    {
        if (String.IsNullOrEmpty(pointInTimeId) || _eventRepository is not ISupportPointInTime repository)
            return;

        try
        {
            await repository.ClosePointInTimeAsync(pointInTimeId);
        }
        catch (Exception ex)
        {
            // Expiry is the bounded fallback if best-effort cleanup cannot reach Elasticsearch.
            _logger.LogWarning(ex, "Could not close the event snapshot for task {TaskId}: {Message}", taskId, ex.Message);
        }
    }

    private static CommandOptions<PersistentEvent> SnapshotOptions(int size) => new CommandOptions<PersistentEvent>()
        .SearchAfterPaging(SearchAfterPagingMode.PointInTime)
        // A queued task is stale after this idle period. Each page renews the same lease for active work and retries.
        .SnapshotPagingLifetime(BackgroundTaskService.StaleAfter)
        .PageLimit(size);

    private static string[] GetIds(FindResults<PersistentEvent> results) => results.Hits
        .Select(hit => hit.Id ?? throw new BulkSelectionRejectedException(SnapshotUnavailable())).ToArray();

    private static BulkOperationCheckResult SnapshotUnavailable() => BulkOperationCheckResult.Invalid("selection", SnapshotUnavailableMessage);

    private PinnedEventSelection ReadPinnedSelection(string? state)
    {
        var pinned = state is null ? null : _serializer.Deserialize<PinnedEventSelection>(state);
        // Records created before snapshots were introduced contain only filters and stack ids. Replaying them would
        // broaden the confirmed membership, so those tasks must be restarted instead of silently upgrading in place.
        if (pinned is null || pinned.Version != 1 || (!pinned.IsEmpty && pinned.Ids is null && String.IsNullOrEmpty(pinned.PointInTimeId)))
            throw new BulkSelectionRejectedException(SnapshotUnavailable());

        return pinned;
    }

    private PinnedEventSelection CreatePinnedSelection(AppFilter appFilter)
    {
        var organization = appFilter.Organizations.Single();
        var project = appFilter.Projects?.SingleOrDefault();
        var timeProvider = _configuration.TimeProvider;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var retentionDate = organization.GetRetentionUtcCutoff(_appOptions.MaximumRetentionDays,
            project?.CreatedUtc.SafeSubtract(TimeSpan.FromDays(3)), timeProvider);
        // Keep AppFilterQueryBuilder's date-math rounding, including the inclusive upper day's rounding. Pinning only
        // the submitted time range could admit older events after a retention upgrade or project-scope change.
        double retentionDays = Math.Max(Math.Round(Math.Abs(utcNow.Subtract(retentionDate).TotalDays), MidpointRounding.AwayFromZero), 1);
        string anchor = utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) + "||";
        return new PinnedEventSelection
        {
            Version = 1,
            RetentionStart = FormattableString.Invariant($"{anchor}/d-{(int)retentionDays}d"),
            RetentionEnd = $"{anchor}/d+1d"
        };
    }

    private static RepositoryQuery<PersistentEvent> ApplyScope(RepositoryQuery<PersistentEvent> query, AppFilter appFilter, BulkOperationSelection selection, PinnedEventSelection pinned)
    {
        query.AppFilter(appFilter);
        if (pinned.RetentionStart is not null && pinned.RetentionEnd is not null)
            query.ElasticFilter(new DateRangeQuery { Field = EventIndex.Alias.Date, Gte = pinned.RetentionStart, Lte = pinned.RetentionEnd });

        if (selection.UtcStart.HasValue || selection.UtcEnd.HasValue)
        {
            query.DateRange(selection.UtcStart, selection.UtcEnd, EventIndex.Alias.Date)
                .Index(selection.UtcStart, selection.UtcEnd);
        }

        // The event date is chosen by the client, so an old-dated event can still arrive after the task was created.
        if (selection.UtcCreatedBefore.HasValue)
            query.DateRange(null, selection.UtcCreatedBefore, CreatedUtcField);

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

        // AppFilter turns an unavailable premium search into no matches. Report the plan change explicitly so the
        // worker cannot mistake it for an exhausted selection and claim the deletion completed.
        if (validation.UsesPremiumFeatures && !organization.HasPremiumFeatures)
            return (null, BulkOperationCheckResult.PlanLimit(PremiumSearchUpgradeMessage));

        appFilter.UsesPremiumFeatures = validation.UsesPremiumFeatures;
        return (appFilter, BulkOperationCheckResult.Ok);
    }

    /// <summary>A bounded id set or the query and snapshot that fix the originally confirmed membership.</summary>
    private sealed class PinnedEventSelection
    {
        public int Version { get; set; }
        // Storage serialization omits empty arrays, so an intentionally empty membership needs its own marker.
        public bool IsEmpty { get; set; }
        public string[]? Ids { get; set; }
        public string? PointInTimeId { get; set; }
        public long Total { get; set; }
        public string? RetentionStart { get; set; }
        public string? RetentionEnd { get; set; }
        public string? EventFilter { get; set; }
        public string[] ExcludedStackIds { get; set; } = [];
        public string[] StackIds { get; set; } = [];
    }

    private sealed class SelectionCursor
    {
        public string? After { get; set; }
        public string? PointInTimeId { get; set; }
        public long Position { get; set; }
        public bool Exhausted { get; set; }
    }
}
