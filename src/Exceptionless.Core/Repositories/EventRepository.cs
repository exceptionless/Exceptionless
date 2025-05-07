using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using FluentValidation;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Nest;

namespace Exceptionless.Core.Repositories;

public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository
{
    private readonly TimeProvider _timeProvider;

    public EventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, IValidator<PersistentEvent> validator)
        : base(configuration.Events, validator, options)
    {
        _timeProvider = configuration.TimeProvider;

        AutoCreateCustomFields = true;

        DisableCache(); // NOTE: If cache is ever enabled, then fast paths for patching/deleting with scripts will be super slow!
        BatchNotifications = true;
        DefaultPipeline = "events-pipeline";

        // copy to fields
        AddDefaultExclude(EventIndex.Alias.IpAddress);
        AddDefaultExclude(EventIndex.Alias.OperatingSystem);
        AddDefaultExclude(EventIndex.Alias.Error);

        AddPropertyRequiredForRemove(e => e.Date);
    }

    public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && !Query<PersistentEvent>.Exists(f => f.Field(e => e.Idx[Event.KnownDataKeys.SessionEnd + "-d"]));
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
        if (stackIds.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(stackIds));

        return RemoveAllAsync(q => q.Stack(stackIds));
    }

    protected override string? GetTenantKey(IRepositoryQuery query)
    {
        var organizations = query.GetOrganizations();
        if (organizations.Count != 1)
            return null;

        return organizations.Single();
    }

    protected override async Task<CustomFieldDefinition?> HandleUnmappedCustomField(PersistentEvent document, string name, object value, IDictionary<string, CustomFieldDefinition> existingFields)
    {
        if (!AutoCreateCustomFields)
            return null;

        if (name.StartsWith('@'))
            return null;

        var tenantKey = GetDocumentTenantKey(document);
        if (String.IsNullOrEmpty(tenantKey))
            return null;

        string fieldType = GetTermType(value);
        if (fieldType == String.Empty)
            return null;

        return await ElasticIndex.Configuration.CustomFieldDefinitionRepository.AddFieldAsync(EntityTypeName, GetDocumentTenantKey(document), "data." + name, fieldType);
    }

    private static string GetTermType(object term)
    {
        if (term is string stringTerm)
        {
            if (Boolean.TryParse(stringTerm, out var _))
                return BooleanFieldType.IndexType;

            if (DateTime.TryParse(stringTerm, out var _))
                return DateFieldType.IndexType;

            return StringFieldType.IndexType;
        }
        else if (term is Int32)
        {
            return IntegerFieldType.IndexType;
        }
        else if (term is Int64)
        {
            return LongFieldType.IndexType;
        }
        else if (term is Double)
        {
            return DoubleFieldType.IndexType;
        }
        else if (term is float)
        {
            return FloatFieldType.IndexType;
        }
        else if (term is bool)
        {
            return BooleanFieldType.IndexType;
        }
        else if (term is DateTime or DateTimeOffset or DateOnly)
        {
            return DateFieldType.IndexType;
        }

        return String.Empty;
    }
}
