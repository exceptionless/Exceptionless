using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Validation;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories;

public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository
{
    private const string StackIdsAggregation = "stack_ids";
    private const string CompositeStackIdSource = "stack_id";
    private const string StackCountAggregation = "stack_count";
    private readonly TimeProvider _timeProvider;

    public EventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, MiniValidationValidator validator)
        : base(configuration.Events, validator, options)
    {
        _timeProvider = configuration.TimeProvider;

        DisableCache(); // NOTE: If cache is ever enabled, then fast paths for patching/deleting with scripts will be super slow!
        BatchNotifications = true;
        DefaultPipeline = "events-pipeline";

        AddDefaultExclude(e => e.Idx);
        // copy to fields
        AddDefaultExclude(EventIndex.Alias.IpAddress);
        AddDefaultExclude(EventIndex.Alias.OperatingSystem);
        AddDefaultExclude(EventIndex.Alias.Error);

        AddRequiredField(e => e.Date);
    }

    public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        var query = new RepositoryQuery<PersistentEvent>()
            .FieldEquals(e => e.Type, Event.KnownTypes.Session)
            .ElasticFilter(new BoolQuery { MustNot = [new ExistsQuery { Field = $"idx.{Event.KnownDataKeys.SessionEnd}-d" }] });

        if (createdBeforeUtc.Ticks > 0)
            query = query.DateRange(null, createdBeforeUtc, (PersistentEvent e) => e.Date); // No lower bound, upper bound is exclusive

        return FindAsync(q => query.SortDescending(e => e.Date), options);
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

    public Task<long> RemoveAllByOrganizationAndClientIpAsync(string organizationId, string? clientIpAddress, DateTime? startUtc, DateTime? endUtc, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var query = new RepositoryQuery<PersistentEvent>().Organization(organizationId);
        if (startUtc.HasValue && endUtc.HasValue)
            query = query.DateRange(startUtc, endUtc, InferField(e => e.Date)).Index(startUtc, endUtc);
        else if (endUtc.HasValue)
            query = query.DateRange(null, endUtc, (PersistentEvent e) => e.Date);
        else if (startUtc.HasValue)
            query = query.DateRange(startUtc, null, (PersistentEvent e) => e.Date);

        if (!String.IsNullOrEmpty(clientIpAddress))
            query = query.FieldEquals(EventIndex.Alias.IpAddress, clientIpAddress);

        return RemoveAllAsync(q => query, options);
    }

    public Task<long> RemoveAllByProjectAndClientIpAsync(string organizationId, string projectId, string clientIpAddress, DateTime? startUtc, DateTime? endUtc, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIpAddress);

        var query = new RepositoryQuery<PersistentEvent>()
            .Organization(organizationId)
            .Project(projectId)
            .FieldEquals(EventIndex.Alias.IpAddress, clientIpAddress);
        if (startUtc.HasValue || endUtc.HasValue)
            query = query.DateRange(startUtc, endUtc, InferField(e => e.Date));
        if (startUtc.HasValue && endUtc.HasValue)
            query = query.Index(startUtc, endUtc);

        return RemoveAllAsync(q => query, options);
    }

    public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId)
    {
        return FindAsync(q => q.Project(projectId).FieldEquals(e => e.ReferenceId, referenceId).SortDescending(e => e.Date), o => o.PageLimit(10));
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
            .Stack(ev.StackId)
            .ExcludedId(ev.Id)
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
            .Stack(ev.StackId)
            .ExcludedId(ev.Id)
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

    public async Task<IdPage> GetStackIdPageAsync(IRepositoryQuery query, string? afterStackId, int size)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var options = ConfigureOptions(new CommandOptions<PersistentEvent>().TrackTotalHits(false).SoftDeleteMode(SoftDeleteQueryMode.ActiveOnly));
        var search = await CreateSearchDescriptorAsync(query, options);
        var composite = new CompositeAggregation
        {
            Size = size,
            Sources = [new KeyValuePair<string, CompositeAggregationSource>(CompositeStackIdSource, new CompositeAggregationSource { Terms = new CompositeTermsAggregation { Field = InferField(e => e.StackId) } })]
        };

        if (!String.IsNullOrEmpty(afterStackId))
            composite.After = new Dictionary<Field, FieldValue> { [CompositeStackIdSource] = afterStackId };

        search.Size(0).Aggregations(a => a.Add(StackIdsAggregation, new Aggregation { Composite = composite }));

        var response = await _client.SearchAsync<PersistentEvent>(search);
        _logger.LogRequest(response, options.GetQueryLogLevel());
        if (!response.IsValidResponse)
            throw new DocumentException($"Error while paging stack ids: {response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation}", response.ApiCallDetails.OriginalException);

        EnsureComplete(response.Shards, response.TimedOut, "paging stack ids");

        var buckets = response.Aggregations?.GetComposite(StackIdsAggregation)?.Buckets ?? [];
        string[] stackIds = buckets.Select(b => b.Key[CompositeStackIdSource].ToString()!).ToArray();
        return new IdPage(stackIds, stackIds.Length >= size ? stackIds[^1] : null);
    }

    public async Task<IdPage> GetEventIdPageAsync(IRepositoryQuery query, string? cursor, int size)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var options = ConfigureOptions(new CommandOptions<PersistentEvent>().TrackTotalHits(false).SoftDeleteMode(SoftDeleteQueryMode.ActiveOnly));
        var search = await CreateSearchDescriptorAsync(query, options);
        search.Size(size).Source(false);
        if (!String.IsNullOrEmpty(cursor))
            search.SearchAfter(DecodeSortValues(cursor));

        var response = await _client.SearchAsync<PersistentEvent>(search);
        _logger.LogRequest(response, options.GetQueryLogLevel());
        if (!response.IsValidResponse)
            throw new DocumentException($"Error while paging event ids: {response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation}", response.ApiCallDetails.OriginalException);

        EnsureComplete(response.Shards, response.TimedOut, "paging event ids");

        string[] ids = response.Hits.Where(h => !String.IsNullOrEmpty(h.Id)).Select(h => h.Id!).ToArray();
        var last = response.Hits.LastOrDefault();
        return new IdPage(ids, ids.Length > 0 && last?.Sort is not null ? EncodeSortValues(last.Sort) : null);
    }

    public async Task<long> CountStacksAsync(IRepositoryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var options = ConfigureOptions(new CommandOptions<PersistentEvent>().TrackTotalHits(false).SoftDeleteMode(SoftDeleteQueryMode.ActiveOnly));
        var search = await CreateSearchDescriptorAsync(query, options);
        search.Size(0).Aggregations(a => a.Add(StackCountAggregation, new Aggregation { Cardinality = new CardinalityAggregation { Field = InferField(e => e.StackId) } }));

        var response = await _client.SearchAsync<PersistentEvent>(search);
        _logger.LogRequest(response, options.GetQueryLogLevel());
        if (!response.IsValidResponse)
            throw new DocumentException($"Error while counting stacks: {response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation}", response.ApiCallDetails.OriginalException);

        EnsureComplete(response.Shards, response.TimedOut, "counting stacks");

        double? count = response.Aggregations?.GetCardinality(StackCountAggregation)?.Value;
        return count.HasValue ? Convert.ToInt64(count.Value) : 0;
    }

    /// <summary>
    /// A search can answer 200 with some shards failed or the search timed out, and then holds only part of the matches. Applying a
    /// partial page would skip the rest while the task reports success, so such a page is an error and the worker retries it.
    /// </summary>
    public static void EnsureComplete(ShardStatistics? shards, bool? timedOut, string operation)
    {
        if (timedOut is true)
            throw new DocumentException($"The search timed out while {operation}; the result may be incomplete.");

        if (shards is not null && shards.Failed > 0)
            throw new DocumentException($"{shards.Failed} of {shards.Total} shards failed while {operation}; the result may be incomplete.");
    }

    // The cursor has the same format as Foundatio's search after token (base64 of the JSON array of sort values), so a task that
    // started with a token Foundatio wrote keeps running and tokens written here are readable by Foundatio.
    private static string EncodeSortValues(IEnumerable<FieldValue> values)
    {
        object[] sortValues = values.Select(v => v.TryGetLong(out long? l) && l.HasValue ? (object)l.Value : v.TryGetString(out string? str) && str is not null ? str : throw new NotSupportedException("Unsupported sort value.")).ToArray();
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(sortValues)));
    }

    private static List<FieldValue> DecodeSortValues(string cursor)
    {
        var values = new List<FieldValue>();
        foreach (var element in System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(Convert.FromBase64String(cursor))!)
            values.Add(element.ValueKind is System.Text.Json.JsonValueKind.Number ? FieldValue.Long(element.GetInt64()) : FieldValue.String(element.GetString()!));

        return values;
    }

    public Task<long> RemoveAllByStackIdsAsync(string[] stackIds)
    {
        ArgumentNullException.ThrowIfNull(stackIds);
        if (stackIds.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(stackIds));

        return RemoveAllAsync(q => q.Stack(stackIds));
    }
}
