using System.Linq.Expressions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Validation;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories;

public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository
{
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public EventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, MiniValidationValidator validator)
        : base(configuration.Events, validator, options)
    {
        _configuration = configuration;
        _timeProvider = configuration.TimeProvider;

        DisableCache(); // NOTE: If cache is ever enabled, then fast paths for patching/deleting with scripts will be super slow!
        BatchNotifications = true;
        DefaultPipeline = "events-pipeline";

        AddDefaultExclude(e => e.Idx!);
        // copy to fields
        AddDefaultExclude(EventIndex.Alias.IpAddress);
        AddDefaultExclude(EventIndex.Alias.OperatingSystem);
        AddDefaultExclude(EventIndex.Alias.Error);

        AddRequiredField(e => e.Date);
    }

    public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && !Query<PersistentEvent>.Exists(f => f.Field(e => e.Idx![Event.KnownDataKeys.SessionEnd + "-d"]));
        if (createdBeforeUtc.Ticks > 0)
            filter &= Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThanOrEquals(createdBeforeUtc));

        return FindAsync(q => q.ElasticFilter(filter).SortDescending(e => e.Date), options);
    }

    /// <summary>
    /// Updates the session start last activity time if the id is a valid session start event.
    /// </summary>
    public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true)
    {
        var ev = await GetByIdAsync(id);
        if (ev is null)
            return false;

        if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd))
            return false;

        await SaveAsync(ev, o => o.Notifications(sendNotifications));
        return true;
    }

    public Task<long> RemoveAllAsync(string organizationId, string? clientIpAddress, DateTime? utcStart, DateTime? utcEnd, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var query = new RepositoryQuery<PersistentEvent>().Organization(organizationId);
        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.DateRange(utcStart, utcEnd, InferField(e => e.Date)).Index(utcStart, utcEnd);
        else if (utcEnd.HasValue)
            query = query.ElasticFilter(Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThan(utcEnd)));
        else if (utcStart.HasValue)
            query = query.ElasticFilter(Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).GreaterThan(utcStart)));

        if (!String.IsNullOrEmpty(clientIpAddress))
            query = query.FieldEquals(EventIndex.Alias.IpAddress, clientIpAddress);

        return RemoveAllAsync(q => query, options);
    }

    public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId)
    {
        var filter = Query<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
        return FindAsync(q => q.Project(projectId).ElasticFilter(filter).SortDescending(e => e.Date), o => o.PageLimit(10));
    }

    public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        var previous = GetPreviousEventIdAsync(ev, systemFilter, utcStart, utcEnd);
        var next = GetNextEventIdAsync(ev, systemFilter, utcStart, utcEnd);
        await Task.WhenAll(previous, next);

        return new PreviousAndNextEventIdResult
        {
            Previous = previous.Result,
            Next = next.Result
        };
    }

    private async Task<string?> GetPreviousEventIdAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        var retentionDate = _options.MaximumRetentionDays > 0 ? _timeProvider.GetUtcNow().UtcDateTime.Date.SubtractDays(_options.MaximumRetentionDays) : DateTime.MinValue;
        if (!utcStart.HasValue || utcStart.Value.IsBefore(retentionDate))
            utcStart = retentionDate;

        if (!utcEnd.HasValue || utcEnd.Value.IsAfter(ev.Date.UtcDateTime))
            utcEnd = ev.Date.UtcDateTime;

        var utcEventDate = ev.Date.UtcDateTime;
        // utcEnd is before the current event date.
        if (utcStart > utcEventDate || utcEnd < utcEventDate)
            return null;

        var results = await FindAsync(q => q
            .DateRange(utcStart, utcEventDate, (PersistentEvent e) => e.Date)
            .Index(utcStart, utcEventDate)
            .SortDescending(e => e.Date)
            .Include(e => e.Id, e => e.Date)
            .AppFilter(systemFilter)
            .ElasticFilter(!Query<PersistentEvent>.Ids(ids => ids.Values(ev.Id)))
            .FilterExpression(String.Concat(EventIndex.Alias.StackId, ":", ev.StackId))
            .EnforceEventStackFilter(false), o => o.PageLimit(10));

        if (results.Total == 0)
            return null;

        // make sure we don't have records with the exact same occurrence date
        if (results.Documents.All(t => t.Date != ev.Date))
            return results.Documents.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).First().Id;

        // we have records with the exact same occurrence date, we need to figure out the order of those
        // put our target error into the mix, sort it and return the result before the target
        var unionResults = results.Documents.Union([ev])
            .OrderBy(t => t.Date.UtcTicks).ThenBy(t => t.Id)
            .ToList();

        int index = unionResults.FindIndex(t => t.Id == ev.Id);
        return index == 0 ? null : unionResults[index - 1].Id;
    }

    private async Task<string?> GetNextEventIdAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        if (!utcStart.HasValue || utcStart.Value.IsBefore(ev.Date.UtcDateTime))
            utcStart = ev.Date.UtcDateTime;

        if (!utcEnd.HasValue || utcEnd.Value.IsAfter(_timeProvider.GetUtcNow().UtcDateTime))
            utcEnd = _timeProvider.GetUtcNow().UtcDateTime;

        var utcEventDate = ev.Date.UtcDateTime;
        // utcEnd is before the current event date.
        if (utcStart > utcEventDate || utcEnd < utcEventDate)
            return null;

        var results = await FindAsync(q => q
            .DateRange(utcEventDate, utcEnd, (PersistentEvent e) => e.Date)
            .Index(utcEventDate, utcEnd)
            .SortAscending(e => e.Date)
            .Include(e => e.Id, e => e.Date)
            .AppFilter(systemFilter)
            .ElasticFilter(!Query<PersistentEvent>.Ids(ids => ids.Values(ev.Id)))
            .FilterExpression(String.Concat(EventIndex.Alias.StackId, ":", ev.StackId))
            .EnforceEventStackFilter(false), o => o.PageLimit(10));

        if (results.Total == 0)
            return null;

        // make sure we don't have records with the exact same occurrence date
        if (results.Documents.All(t => t.Date != ev.Date))
            return results.Documents.OrderBy(t => t.Date).ThenBy(t => t.Id).First().Id;

        // we have records with the exact same occurrence date, we need to figure out the order of those
        // put our target error into the mix, sort it and return the result after the target
        var unionResults = results.Documents.Union([ev])
            .OrderBy(t => t.Date.Ticks).ThenBy(t => t.Id)
            .ToList();

        int index = unionResults.FindIndex(t => t.Id == ev.Id);
        return index == unionResults.Count - 1 ? null : unionResults[index + 1].Id;
    }

    public override Task<FindResults<PersistentEvent>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return FindAsync(q => q.Organization(organizationId).SortDescending(e => e.Date).SortDescending(e => e.Id), options);
    }

    public override Task<FindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        return FindAsync(q => q.Project(projectId).SortDescending(e => e.Date).SortDescending(e => e.Id), options);
    }

    public Task<long> RemoveAllByStackIdsAsync(string[] stackIds)
    {
        ArgumentNullException.ThrowIfNull(stackIds);
        if (stackIds is [])
            throw new ArgumentOutOfRangeException(nameof(stackIds));

        return RemoveAllAsync(q => q.Stack(stackIds));
    }

    public Task<long> RemoveAllByProjectIdsAsync(string[] projectIds)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds is [])
            throw new ArgumentOutOfRangeException(nameof(projectIds));

        return RemoveAllAsync(q => q.Project(projectIds));
    }

    public Task<long> RemoveAllByOrganizationIdsAsync(string[] organizationIds)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);
        if (organizationIds is [])
            throw new ArgumentOutOfRangeException(nameof(organizationIds));

        return RemoveAllAsync(q => q.Organization(organizationIds));
    }

    /// <summary>
    /// Reassigns all events from the source stacks to the target stack using a parameterized
    /// Painless script (no string interpolation) to prevent script injection.
    /// </summary>
    public Task<long> ReassignStackAsync(IEnumerable<string> sourceStackIds, string targetStackId)
    {
        ArgumentNullException.ThrowIfNull(sourceStackIds);
        ArgumentException.ThrowIfNullOrEmpty(targetStackId);

        // Materialize to avoid multiple enumeration and guard against empty — an empty
        // .Stack() filter would match ALL events and reassign them to the target stack.
        var sourceIds = sourceStackIds.ToList();
        if (sourceIds.Count == 0)
            return Task.FromResult(0L);

        return PatchAllAsync(
            q => q.Stack(sourceIds),
            new ScriptPatch("ctx._source.stack_id = params.targetStackId")
            {
                Params = new Dictionary<string, object> { ["targetStackId"] = targetStackId }
            });
    }

    public Task<IReadOnlyCollection<string>> GetDistinctStackIdsAsync(int batchSize, CompositeKeyResult? afterKey = null)
    {
        return GetDistinctFieldValuesAsync("stack_id", e => e.StackId, batchSize, afterKey);
    }

    public Task<IReadOnlyCollection<string>> GetDistinctProjectIdsAsync(int batchSize, CompositeKeyResult? afterKey = null)
    {
        return GetDistinctFieldValuesAsync("project_id", e => e.ProjectId, batchSize, afterKey);
    }

    public Task<IReadOnlyCollection<string>> GetDistinctOrganizationIdsAsync(int batchSize, CompositeKeyResult? afterKey = null)
    {
        return GetDistinctFieldValuesAsync("organization_id", e => e.OrganizationId, batchSize, afterKey);
    }

    /// <summary>
    /// Uses a composite aggregation to paginate through all distinct values of a field.
    /// Composite aggregations are preferred over terms aggregations for high-cardinality fields
    /// because terms aggregations can silently miss values when the unique count exceeds the
    /// configured size parameter. Composite aggregations guarantee correct iteration via an
    /// after_key cursor, at the cost of requiring sequential page fetches.
    /// </summary>
    private async Task<IReadOnlyCollection<string>> GetDistinctFieldValuesAsync(
        string fieldName, Expression<Func<PersistentEvent, object>> fieldExpression, int batchSize, CompositeKeyResult? afterKey)
    {
        var afterKeyValues = afterKey?.AfterKey;
        var search = await _configuration.Client.SearchAsync<PersistentEvent>(s =>
        {
            s.Size(0).Aggregations(a => a
                .Composite($"composite_{fieldName}", c =>
                {
                    var composite = c.Size(batchSize)
                        .Sources(src => src.Terms(fieldName, t => t.Field(fieldExpression)));
                    if (afterKeyValues is { Count: > 0 })
                        composite.After(new CompositeKey(afterKeyValues));
                    return composite;
                }));
            return s;
        });

        var composite = search.Aggregations.Composite($"composite_{fieldName}");

        // Always clear the cursor first; repopulate only when a next page exists.
        // This ensures callers that check afterKey.AfterKey.Count > 0 correctly
        // detect end-of-pagination without requiring a final empty-result fetch.
        if (afterKey is not null)
        {
            afterKey.AfterKey.Clear();
            if (composite?.AfterKey is not null)
            {
                foreach (var kvp in composite.AfterKey)
                    afterKey.AfterKey[kvp.Key] = kvp.Value;
            }
        }

        if (composite?.Buckets is not { Count: > 0 })
            return [];

        return composite.Buckets.Select(b => b.Key[fieldName].ToString()!).ToArray();
    }
}
