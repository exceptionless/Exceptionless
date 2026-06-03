using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Validation;
using Exceptionless.DateTimeExtensions;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Nest;

namespace Exceptionless.Core.Repositories;

public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository
{
    private const string LegacySessionEndIdxField = "sessionend-d";
    private readonly TimeProvider _timeProvider;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;

    public EventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, MiniValidationValidator validator, IProjectRepository projectRepository, IStackRepository stackRepository)
        : base(configuration.Events, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;

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
        var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session)
            && !Query<PersistentEvent>.Exists(f => f.Field(e => e.Idx![EventCustomFieldService.SessionEndIdxField]))
            && !Query<PersistentEvent>.Exists(f => f.Field($"idx.{LegacySessionEndIdxField}"));
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

        if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd, hasError))
            return false;

        await SaveAsync(ev, o => o.Notifications(sendNotifications));
        return true;
    }

    public Task<long> RemoveAllAsync(string organizationId, string? clientIpAddress, DateTime? utcStart, DateTime? utcEnd, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var query = new RepositoryQuery<PersistentEvent>().Organization(organizationId);
        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.DateRange(utcStart, utcEnd, InferField(e => e.Date));
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

    /// <summary>
    /// Override to prevent the base from clearing idx (which would destroy slot values populated by EventCustomFieldService).
    /// Custom field indexing is handled externally by EventCustomFieldService.
    /// </summary>
    protected override Task OnCustomFieldsDocumentsChanging(object sender, DocumentsChangeEventArgs<PersistentEvent> args)
        => Task.CompletedTask;

    /// <summary>
    /// Resolve the tenant key from the query's organization filter.
    /// </summary>
    protected override string? GetTenantKey(IRepositoryQuery query)
    {
        var organizationIds = query.GetOrganizations();
        return organizationIds.Count == 1 ? organizationIds.First() : null;
    }

    /// <summary>
    /// Custom field query resolution: resolves idx.fieldName and data.fieldName to idx.{type}-{slot}.
    /// Blocks raw slot access (e.g., idx.keyword-7) to prevent querying deleted or other tenants' fields.
    /// Returns null for non-idx/data fields so the global resolver (field aliases) still works.
    /// </summary>
    // Well-known system field slot mappings (deterministic — always slot 1 for each type).
    private static readonly Dictionary<string, string> _systemFieldSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@ref:session"] = EventCustomFieldService.SessionReferenceIdxField,
        [Event.KnownDataKeys.SessionEnd] = EventCustomFieldService.SessionEndIdxField,
        [Event.KnownDataKeys.SessionHasError] = EventCustomFieldService.SessionHasErrorIdxField,
    };

    protected override async Task OnCustomFieldsBeforeQuery(object sender, BeforeQueryEventArgs<PersistentEvent> args)
    {
        var tenantKey = await ResolveTenantKeyAsync(args.Query);

        var definitionRepo = ElasticIndex.Configuration.CustomFieldDefinitionRepository;

        // Lazy-load field mapping only when a query actually references idx.* or data.* fields.
        // Most queries (count, date histograms, simple filters) never hit custom fields,
        // so deferring this avoids a cache/ES lookup on every hot-path query.
        Dictionary<string, string>? mapping = null;

        args.Options.QueryFieldResolver(async (field, _) =>
        {
            string? fieldName = null;
            if (field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase))
                fieldName = field.Substring(4);
            else if (field.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
                fieldName = field.Substring(5);
            else if (field.StartsWith("ref.", StringComparison.OrdinalIgnoreCase))
                fieldName = $"@ref:{field.Substring(4)}";

            if (fieldName is null)
                return null;

            // System fields have deterministic slots that don't require tenant resolution.
            if (_systemFieldSlots.TryGetValue(fieldName, out var systemSlot))
                return $"idx.{systemSlot}";

            // Non-system fields require a tenant key to look up their slot assignment.
            if (String.IsNullOrEmpty(tenantKey) || definitionRepo is null)
            {
                // Without tenant context, block raw idx access; data.* fields fall through.
                return field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase) ? "idx.__blocked__" : null;
            }

            mapping ??= (await definitionRepo.GetFieldMappingAsync(EntityTypeName, tenantKey))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetIdxName(), StringComparer.OrdinalIgnoreCase);

            if (mapping.TryGetValue(fieldName, out var idxName))
                return $"idx.{idxName}";

            // Block raw slot access (e.g., idx.keyword-7) and unknown idx fields.
            // Redirect to a non-existent field so the clause matches nothing.
            if (field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase))
                return "idx.__blocked__";

            // For data.* and ref.* fields that don't map to a custom field, return null to let
            // other resolvers handle legitimate data paths (e.g., data.@version).
            return null;
        });
    }

    private async Task<string?> ResolveTenantKeyAsync(IRepositoryQuery query)
    {
        var organizationIds = query.GetOrganizations();
        if (organizationIds.Count == 1)
            return organizationIds.Single();

        var projectIds = query.GetProjects();
        if (projectIds.Count == 1)
            return (await _projectRepository.GetByIdAsync(projectIds.Single()))?.OrganizationId;

        var stackIds = query.GetStacks();
        if (stackIds.Count == 1)
            return (await _stackRepository.GetByIdAsync(stackIds.Single()))?.OrganizationId;

        return null;
    }
}
