using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Esql;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public interface IStackRollupSearchService
{
    Task<bool> RequiresLookupJoinAsync(string? filter, CancellationToken cancellationToken = default);
    Task<EventLookupSearchResult> SearchEventsAsync(EventLookupSearchRequest request, CancellationToken cancellationToken = default);
    Task<CountResult> CountEventsAsync(EventLookupCountRequest request, CancellationToken cancellationToken = default);
    Task<StackRollupSearchResult> SearchAsync(StackRollupSearchRequest request, CancellationToken cancellationToken = default);
    Task<StackRollupStatsResult> GetStatsAsync(StackRollupStatsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, long>> GetProjectUserCountsAsync(StackRollupProjectUsersRequest request, CancellationToken cancellationToken = default);
}

public sealed record EventLookupSearchRequest(
    AppFilter? AppFilter,
    DateTime UtcStart,
    DateTime UtcEnd,
    string? TimeExpression,
    string? Filter,
    string? Sort,
    int Limit,
    string? Before,
    string? After,
    bool IncludeTotal);

public sealed record EventLookupSearchResult(
    IReadOnlyCollection<string> EventIds,
    bool HasMore,
    long? Total,
    string? Before,
    string? After);

public sealed record EventLookupCountRequest(
    AppFilter? AppFilter,
    DateTime UtcStart,
    DateTime UtcEnd,
    TimeSpan Offset,
    string? Filter,
    string? Aggregations,
    int BucketCount = 50);

public sealed record StackRollupSearchRequest(
    AppFilter? AppFilter,
    DateTime UtcStart,
    DateTime UtcEnd,
    TimeSpan Offset,
    string? TimeExpression,
    string? Filter,
    string? Sort,
    int Limit,
    string? Before,
    string? After,
    bool IncludeTotal);

public sealed record StackRollupSearchResult(
    IReadOnlyCollection<StackRollupRow> Rows,
    bool HasMore,
    long? Total,
    string? Before,
    string? After);

public sealed record StackRollupStatsRequest(
    AppFilter? AppFilter,
    DateTime UtcStart,
    DateTime UtcEnd,
    TimeSpan Offset,
    string? Filter,
    int BucketCount = 50);

public sealed record StackRollupStatsResult(
    long TotalEvents,
    long TotalStacks,
    long NewStacks,
    IReadOnlyCollection<StackRollupStatsBucket> Buckets,
    long Documents = 0);

public sealed record StackRollupStatsBucket(DateTime Date, long Events, long Stacks, long Documents = 0);

public sealed record StackRollupProjectUsersRequest(
    AppFilter AppFilter,
    DateTime UtcStart,
    DateTime UtcEnd,
    IReadOnlyCollection<string> ProjectIds);

public sealed record StackRollupRow(
    string StackId,
    long Total,
    long Users,
    DateTime FirstOccurrence,
    DateTime LastOccurrence);

public sealed class InvalidStackRollupCursorException(string message) : Exception(message);
public sealed class InvalidEventLookupCursorException(string message) : Exception(message);

public sealed class StackRollupSearchService : IStackRollupSearchService
{
    private const int CursorVersion = 1;
    private static readonly TimeSpan ReadinessCacheDuration = TimeSpan.FromMinutes(1);
    private readonly ElasticsearchClient _client;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly EventStackFilter _eventStackFilter = new();
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _readinessLock = new(1, 1);
    private StackRollupReadiness? _readiness;
    private DateTimeOffset _readinessExpiresUtc;

    public StackRollupSearchService(
        ElasticsearchClient client,
        ExceptionlessElasticConfiguration configuration,
        TimeProvider timeProvider,
        JsonSerializerOptions serializerOptions,
        ILoggerFactory loggerFactory)
    {
        _client = client;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _serializerOptions = serializerOptions;
        _logger = loggerFactory.CreateLogger<StackRollupSearchService>();
    }

    public async Task<bool> RequiresLookupJoinAsync(string? filter, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(filter))
            return false;

        var stackFilter = await _eventStackFilter.GetStackFilterAsync(StripAlternateInversion(filter));
        return stackFilter?.HasStackOnlyCriteria == true;
    }

    public async Task<EventLookupSearchResult> SearchEventsAsync(EventLookupSearchRequest request, CancellationToken cancellationToken = default)
    {
        var sort = GetEventSort(request.Sort);
        await EnsureReadyAsync(cancellationToken);

        string fingerprint = CreateEventFingerprint(request, sort.Value);
        EventLookupCursor? cursor = DecodeEventCursor(request.Before ?? request.After, sort.Value, fingerprint);
        DateTime utcStart = cursor is null ? request.UtcStart : new DateTime(cursor.UtcStart, DateTimeKind.Utc);
        DateTime utcEnd = cursor is null ? request.UtcEnd : new DateTime(cursor.UtcEnd, DateTimeKind.Utc);
        string normalizedFilter = StripAlternateInversion(request.Filter);
        string? eventFilter = await _eventStackFilter.GetEventFilterAsync(normalizedFilter);
        string? stackFilter = (await _eventStackFilter.GetStackFilterAsync(normalizedFilter))?.Filter;
        Query? sourceFilter = await BuildSourceFilterAsync(request.AppFilter, utcStart, utcEnd, eventFilter);
        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        string query = BuildEventQuery(request, sort, cursor, stackFilter, parameters);
        var rows = await ExecuteEventRowsAsync(query, sourceFilter, parameters, cancellationToken);
        long? total = request.IncludeTotal ? rows.FirstOrDefault()?.TotalEvents : null;
        if (request.IncludeTotal && total is null)
            total = await ExecuteJoinedEventTotalAsync(stackFilter, sourceFilter, cancellationToken);

        bool isBefore = request.Before is not null;
        bool hasExtra = rows.Count > request.Limit;
        if (hasExtra)
            rows.RemoveAt(rows.Count - 1);
        if (isBefore)
            rows.Reverse();

        bool hasPrevious = rows.Count > 0 && (isBefore ? hasExtra : request.After is not null);
        bool hasNext = rows.Count > 0 && (isBefore || hasExtra);
        return new EventLookupSearchResult(
            rows.Select(row => row.EventId).ToArray(),
            hasNext,
            total,
            hasPrevious ? EncodeEventCursor(rows[0], sort, utcStart, utcEnd, fingerprint) : null,
            hasNext ? EncodeEventCursor(rows[^1], sort, utcStart, utcEnd, fingerprint) : null);
    }

    public async Task<CountResult> CountEventsAsync(EventLookupCountRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        string normalizedFilter = StripAlternateInversion(request.Filter);
        string? eventFilter = await _eventStackFilter.GetEventFilterAsync(normalizedFilter);
        string? stackFilter = (await _eventStackFilter.GetStackFilterAsync(normalizedFilter))?.Filter;
        Query? sourceFilter = await BuildSourceFilterAsync(request.AppFilter, request.UtcStart, request.UtcEnd, eventFilter);

        if (String.IsNullOrWhiteSpace(request.Aggregations))
            return new CountResult(await ExecuteJoinedEventTotalAsync(stackFilter, sourceFilter, cancellationToken));

        if (IsTermsTagsAggregation(request.Aggregations))
            return await CountEventTagsAsync(stackFilter, sourceFilter, cancellationToken);

        if (!IsDashboardAggregation(request.Aggregations))
            throw new NotSupportedException("Stack filters currently support the event dashboard aggregations or terms:tags.");

        var stats = await GetStatsAsync(new StackRollupStatsRequest(
            request.AppFilter,
            request.UtcStart,
            request.UtcEnd,
            request.Offset,
            request.Filter,
            request.BucketCount), cancellationToken);
        return ToCountResult(stats);
    }

    public async Task<StackRollupSearchResult> SearchAsync(StackRollupSearchRequest request, CancellationToken cancellationToken = default)
    {
        var sort = GetSort(request.Sort);
        await EnsureReadyAsync(cancellationToken);

        string fingerprint = CreateFingerprint(request);
        StackRollupCursor? cursor = DecodeCursor(request.Before ?? request.After, sort.Value, fingerprint);
        DateTime utcStart = cursor is null ? request.UtcStart : new DateTime(cursor.UtcStart, DateTimeKind.Utc);
        DateTime utcEnd = cursor is null ? request.UtcEnd : new DateTime(cursor.UtcEnd, DateTimeKind.Utc);
        string normalizedFilter = StripAlternateInversion(request.Filter);
        string? eventFilter = await _eventStackFilter.GetEventFilterAsync(normalizedFilter);
        string? stackFilter = (await _eventStackFilter.GetStackFilterAsync(normalizedFilter))?.Filter;
        Query? sourceFilter = await BuildSourceFilterAsync(request.AppFilter, utcStart, utcEnd, eventFilter);

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Executing ES|QL stack rollup sort {Sort}, direction {Direction}, range {UtcStart:o} to {UtcEnd:o}, event filter {HasEventFilter}, stack filter {HasStackFilter}",
            sort.Value,
            request.Before is not null ? "before" : request.After is not null ? "after" : "initial",
            utcStart,
            utcEnd,
            !String.IsNullOrWhiteSpace(eventFilter),
            !String.IsNullOrWhiteSpace(stackFilter));

        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        string query = BuildQuery(request, sort, cursor, stackFilter, parameters, countOnly: false);
        var rows = await ExecuteRowsAsync(query, sourceFilter, parameters, cancellationToken);
        long? total = request.IncludeTotal ? rows.FirstOrDefault()?.TotalStacks : null;

        if (request.IncludeTotal && total is null)
        {
            parameters.Clear();
            string countQuery = BuildQuery(request, sort, cursor: null, stackFilter, parameters, countOnly: true);
            total = await ExecuteTotalAsync(countQuery, sourceFilter, parameters, cancellationToken);
        }

        bool isBefore = request.Before is not null;
        bool hasExtra = rows.Count > request.Limit;
        if (hasExtra)
            rows.RemoveAt(rows.Count - 1);

        if (isBefore)
            rows.Reverse();

        bool hasPrevious = rows.Count > 0 && (isBefore ? hasExtra : request.After is not null);
        bool hasNext = rows.Count > 0 && (isBefore || hasExtra);
        string? before = hasPrevious ? EncodeCursor(rows[0], sort, utcStart, utcEnd, fingerprint) : null;
        string? after = hasNext ? EncodeCursor(rows[^1], sort, utcStart, utcEnd, fingerprint) : null;

        _logger.LogDebug(
            "Completed ES|QL stack rollup sort {Sort}, direction {Direction}, rows {RowCount}, has more {HasMore}, duration {DurationMs}ms",
            sort.Value,
            isBefore ? "before" : request.After is not null ? "after" : "initial",
            rows.Count,
            hasNext,
            stopwatch.Elapsed.TotalMilliseconds);

        return new StackRollupSearchResult(
            rows.Select(ToPublicRow).ToArray(),
            hasNext,
            total,
            before,
            after);
    }

    public async Task<StackRollupStatsResult> GetStatsAsync(StackRollupStatsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        string normalizedFilter = StripAlternateInversion(request.Filter);
        string? eventFilter = await _eventStackFilter.GetEventFilterAsync(normalizedFilter);
        string? stackFilter = (await _eventStackFilter.GetStackFilterAsync(normalizedFilter))?.Filter;
        Query? sourceFilter = await BuildSourceFilterAsync(request.AppFilter, request.UtcStart, request.UtcEnd, eventFilter);
        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        string query = BuildStatsQuery(request, stackFilter, parameters);

        using var response = await _client.Esql.QueryAsync(new EsqlQueryRequest(query)
        {
            AllowPartialResults = false,
            Columnar = false,
            Filter = sourceFilter,
            Format = EsqlFormat.Json,
            Params = new Union<ICollection<ICollection<FieldValue>>, ICollection<KeyValuePair<string, ICollection<FieldValue>>>>(parameters)
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("Stack rollup stats lookup join failed with Elasticsearch status {StatusCode}", response.ApiCallDetails?.HttpStatusCode);
            throw new ApplicationException("The stack rollup stats query failed.");
        }

        using var document = await JsonDocument.ParseAsync(response.Body, cancellationToken: cancellationToken);
        return ReadStats(document.RootElement);
    }

    public async Task<IReadOnlyDictionary<string, long>> GetProjectUserCountsAsync(StackRollupProjectUsersRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProjectIds.Count == 0)
            return new Dictionary<string, long>();

        await EnsureReadyAsync(cancellationToken);
        Query? sourceFilter = await BuildSourceFilterAsync(request.AppFilter, request.UtcStart, request.UtcEnd, eventFilter: null);
        string eventIndex = ValidateIndexName(_configuration.Events.Name);
        string stackIndex = ValidateIndexName(_configuration.Stacks.Name);
        string userField = EscapeIdentifier(EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, user => user.Identity) + ".keyword");
        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        AddParameter(parameters, "project_ids", request.ProjectIds.Select(FieldValue.String).ToArray());
        string query = new StringBuilder()
            .Append("FROM ").Append(eventIndex)
            .Append(" | KEEP stack_id, project_id, ").Append(userField)
            .Append(" | RENAME project_id AS event_project_id, ").Append(userField).Append(" AS event_user")
            .Append(" | LOOKUP JOIN ").Append(stackIndex).Append(" ON stack_id == id AND is_deleted == false")
            .Append(" | WHERE id IS NOT NULL AND event_project_id IN (?project_ids)")
            .Append(" | STATS users = COUNT_DISTINCT(event_user) BY project_id = event_project_id")
            .Append(" | KEEP project_id, users")
            .ToString();

        using var response = await _client.Esql.QueryAsync(new EsqlQueryRequest(query)
        {
            AllowPartialResults = false,
            Columnar = false,
            Filter = sourceFilter,
            Format = EsqlFormat.Json,
            Params = new Union<ICollection<ICollection<FieldValue>>, ICollection<KeyValuePair<string, ICollection<FieldValue>>>>(parameters)
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("Stack rollup project user lookup join failed with Elasticsearch status {StatusCode}", response.ApiCallDetails?.HttpStatusCode);
            throw new ApplicationException("The stack rollup project user query failed.");
        }

        using var document = await JsonDocument.ParseAsync(response.Body, cancellationToken: cancellationToken);
        return ReadProjectUserCounts(document.RootElement);
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        var readiness = await GetReadinessAsync(cancellationToken);
        if (readiness.IsReady)
            return;

        _logger.LogError("Stack rollup lookup join prerequisite failed: {Reason}", readiness.Reason);
        throw new InvalidOperationException($"The stack rollup lookup join prerequisite failed: {readiness.Reason}.");
    }

    private async Task<Query?> BuildSourceFilterAsync(AppFilter? appFilter, DateTime utcStart, DateTime utcEnd, string? eventFilter)
    {
        var query = new RepositoryQuery<PersistentEvent>()
            .AppFilter(appFilter)
            .DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date)
            .FilterExpression(eventFilter);

        var options = new CommandOptions<PersistentEvent>()
            .TimeProvider(_timeProvider)
            .ElasticIndex(_configuration.Events)
            .DocumentType(typeof(PersistentEvent));
        var context = new QueryBuilderContext<PersistentEvent>(query, options);
        await _configuration.Events.QueryBuilder.BuildAsync(context);
        return context.Filter;
    }

    private string BuildEventQuery(
        EventLookupSearchRequest request,
        EventLookupSort sort,
        EventLookupCursor? cursor,
        string? stackFilter,
        ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters)
    {
        string eventIndex = ValidateIndexName(_configuration.Events.Name);
        string stackIndex = ValidateIndexName(_configuration.Stacks.Name);
        var query = new StringBuilder()
            .Append("FROM ").Append(eventIndex).Append(" METADATA _id")
            .Append(" | KEEP _id, stack_id, date")
            .Append(" | LOOKUP JOIN ").Append(stackIndex)
            .Append(" ON stack_id == id AND is_deleted == false");

        if (!String.IsNullOrWhiteSpace(stackFilter))
        {
            query.Append(" AND QSTR(?stack_filter, {\"default_operator\": \"AND\"})");
            AddParameter(parameters, "stack_filter", FieldValue.String(stackFilter));
        }

        query.Append(" | WHERE id IS NOT NULL");
        if (request.IncludeTotal)
            query.Append(" | INLINE STATS total_events = COUNT(*)");

        bool isBefore = request.Before is not null;
        if (cursor is not null)
        {
            string primaryComparison = isBefore
                ? sort.Ascending ? "<" : ">"
                : sort.Ascending ? ">" : "<";
            string idComparison = isBefore ? "<" : ">";
            query
                .Append(" | WHERE date ").Append(primaryComparison).Append(" TO_DATETIME(?cursor_date)")
                .Append(" OR (date == TO_DATETIME(?cursor_date) AND _id ").Append(idComparison).Append(" ?cursor_event_id)");
            AddParameter(parameters, "cursor_date", FieldValue.String(new DateTime(cursor.Date, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)));
            AddParameter(parameters, "cursor_event_id", FieldValue.String(cursor.EventId));
        }

        bool queryAscending = isBefore ? !sort.Ascending : sort.Ascending;
        query
            .Append(" | SORT date ").Append(queryAscending ? "ASC" : "DESC")
            .Append(", _id ").Append(isBefore ? "DESC" : "ASC")
            .Append(" | LIMIT ").Append(request.Limit + 1)
            .Append(" | KEEP _id, date");
        if (request.IncludeTotal)
            query.Append(", total_events");

        return query.ToString();
    }

    private async Task<List<EventLookupEsqlRow>> ExecuteEventRowsAsync(
        string query,
        Query? sourceFilter,
        ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters,
        CancellationToken cancellationToken)
    {
        using var response = await _client.Esql.QueryAsync(new EsqlQueryRequest(query)
        {
            AllowPartialResults = false,
            Columnar = false,
            Filter = sourceFilter,
            Format = EsqlFormat.Json,
            Params = new Union<ICollection<ICollection<FieldValue>>, ICollection<KeyValuePair<string, ICollection<FieldValue>>>>(parameters)
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("Event lookup join failed with Elasticsearch status {StatusCode}", response.ApiCallDetails?.HttpStatusCode);
            throw new ApplicationException("The event lookup join query failed.");
        }

        using var document = await JsonDocument.ParseAsync(response.Body, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("columns", out var columns) || !document.RootElement.TryGetProperty("values", out var values))
            throw new JsonException("The ES|QL event lookup response did not contain columns and values.");

        var columnIndexes = GetColumnIndexes(columns);
        int idIndex = columnIndexes["_id"];
        int dateIndex = columnIndexes["date"];
        int? totalIndex = columnIndexes.TryGetValue("total_events", out int index) ? index : null;
        return values.EnumerateArray()
            .Select(value => new EventLookupEsqlRow(
                value[idIndex].GetString() ?? throw new JsonException("An event lookup row did not contain an event id."),
                value[dateIndex].GetDateTime().ToUniversalTime(),
                totalIndex.HasValue ? value[totalIndex.Value].GetInt64() : null))
            .ToList();
    }

    private Task<long> ExecuteJoinedEventTotalAsync(string? stackFilter, Query? sourceFilter, CancellationToken cancellationToken)
    {
        string eventIndex = ValidateIndexName(_configuration.Events.Name);
        string stackIndex = ValidateIndexName(_configuration.Stacks.Name);
        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        var query = new StringBuilder()
            .Append("FROM ").Append(eventIndex)
            .Append(" | KEEP stack_id")
            .Append(" | LOOKUP JOIN ").Append(stackIndex)
            .Append(" ON stack_id == id AND is_deleted == false");
        if (!String.IsNullOrWhiteSpace(stackFilter))
        {
            query.Append(" AND QSTR(?stack_filter, {\"default_operator\": \"AND\"})");
            AddParameter(parameters, "stack_filter", FieldValue.String(stackFilter));
        }
        query.Append(" | WHERE id IS NOT NULL | STATS total_events = COUNT(*) | KEEP total_events");
        return ExecuteTotalAsync(query.ToString(), sourceFilter, parameters, cancellationToken);
    }

    private async Task<CountResult> CountEventTagsAsync(string? stackFilter, Query? sourceFilter, CancellationToken cancellationToken)
    {
        string eventIndex = ValidateIndexName(_configuration.Events.Name);
        string stackIndex = ValidateIndexName(_configuration.Stacks.Name);
        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        var query = new StringBuilder()
            .Append("FROM ").Append(eventIndex)
            .Append(" | KEEP stack_id, tags")
            .Append(" | LOOKUP JOIN ").Append(stackIndex)
            .Append(" ON stack_id == id AND is_deleted == false");
        if (!String.IsNullOrWhiteSpace(stackFilter))
        {
            query.Append(" AND QSTR(?stack_filter, {\"default_operator\": \"AND\"})");
            AddParameter(parameters, "stack_filter", FieldValue.String(stackFilter));
        }
        query
            .Append(" | WHERE id IS NOT NULL")
            .Append(" | MV_EXPAND tags")
            .Append(" | STATS tag_total = COUNT(*) BY tag = tags")
            .Append(" | SORT tag_total DESC, tag ASC | LIMIT 1000 | KEEP tag, tag_total");

        using var response = await _client.Esql.QueryAsync(new EsqlQueryRequest(query.ToString())
        {
            AllowPartialResults = false,
            Columnar = false,
            Filter = sourceFilter,
            Format = EsqlFormat.Json,
            Params = new Union<ICollection<ICollection<FieldValue>>, ICollection<KeyValuePair<string, ICollection<FieldValue>>>>(parameters)
        }, cancellationToken);
        if (!response.IsValidResponse)
            throw new ApplicationException("The event tag lookup join query failed.");

        using var document = await JsonDocument.ParseAsync(response.Body, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("columns", out var columns) || !document.RootElement.TryGetProperty("values", out var values))
            throw new JsonException("The ES|QL event tag response did not contain columns and values.");
        var columnIndexes = GetColumnIndexes(columns);
        int tagIndex = columnIndexes["tag"];
        int tagTotalIndex = columnIndexes["tag_total"];
        var buckets = new List<IBucket>();
        foreach (var value in values.EnumerateArray())
        {
            buckets.Add(new KeyedBucket<string>(null)
            {
                Key = value[tagIndex].GetString() ?? String.Empty,
                Total = value[tagTotalIndex].GetInt64(),
                Data = new Dictionary<string, object> { ["@type"] = "string" }
            });
        }

        long total = await ExecuteJoinedEventTotalAsync(stackFilter, sourceFilter, cancellationToken);
        return new CountResult(total, new Dictionary<string, IAggregate>
        {
            ["terms_tags"] = new BucketAggregate
            {
                Items = buckets,
                Data = new Dictionary<string, object> { ["@type"] = "bucket" }
            }
        });
    }

    private string BuildQuery(
        StackRollupSearchRequest request,
        StackRollupSort sort,
        StackRollupCursor? cursor,
        string? stackFilter,
        ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters,
        bool countOnly)
    {
        string eventIndex = ValidateIndexName(_configuration.Events.Name);
        string stackIndex = ValidateIndexName(_configuration.Stacks.Name);
        string userField = EscapeIdentifier(EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, user => user.Identity) + ".keyword");
        var query = new StringBuilder()
            .Append("FROM ").Append(eventIndex)
            .Append(" | KEEP stack_id, count, date, ").Append(userField)
            .Append(" | RENAME ").Append(userField).Append(" AS event_user")
            .Append(" | LOOKUP JOIN ").Append(stackIndex)
            .Append(" ON stack_id == id AND is_deleted == false");

        if (!String.IsNullOrWhiteSpace(stackFilter))
        {
            query.Append(" AND QSTR(?stack_filter, {\"default_operator\": \"AND\"})");
            AddParameter(parameters, "stack_filter", FieldValue.String(stackFilter));
        }

        query
            .Append(" | WHERE id IS NOT NULL")
            .Append(" | STATS event_total = SUM(COALESCE(count, 1)), event_users = COUNT_DISTINCT(event_user), event_first = MIN(date), event_last = MAX(date) BY stack_id");

        if (countOnly)
            return query.Append(" | STATS total_stacks = COUNT(*) | KEEP total_stacks").ToString();

        if (request.IncludeTotal)
            query.Append(" | INLINE STATS total_stacks = COUNT(*)");

        bool isBefore = request.Before is not null;
        if (cursor is not null)
        {
            string primaryComparison = isBefore
                ? sort.Ascending ? "<" : ">"
                : sort.Ascending ? ">" : "<";
            string idComparison = isBefore ? "<" : ">";
            string metricParameter = sort.IsDate ? "TO_DATETIME(?cursor_metric)" : "?cursor_metric";
            query
                .Append(" | WHERE ").Append(sort.Metric).Append(' ').Append(primaryComparison).Append(' ').Append(metricParameter)
                .Append(" OR (").Append(sort.Metric).Append(" == ").Append(metricParameter)
                .Append(" AND stack_id ").Append(idComparison).Append(" ?cursor_stack_id)");
            AddParameter(parameters, "cursor_metric", sort.IsDate
                ? FieldValue.String(new DateTime(cursor.Metric, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture))
                : FieldValue.Long(cursor.Metric));
            AddParameter(parameters, "cursor_stack_id", FieldValue.String(cursor.StackId));
        }

        bool queryAscending = isBefore ? !sort.Ascending : sort.Ascending;
        string primarySort = queryAscending ? "ASC" : "DESC";
        string idSort = isBefore ? "DESC" : "ASC";
        query
            .Append(" | SORT ").Append(sort.Metric).Append(' ').Append(primarySort).Append(", stack_id ").Append(idSort)
            .Append(" | LIMIT ").Append(request.Limit + 1)
            .Append(" | KEEP stack_id, event_total, event_users, event_first, event_last");

        if (request.IncludeTotal)
            query.Append(", total_stacks");

        return query.ToString();
    }

    private string BuildStatsQuery(
        StackRollupStatsRequest request,
        string? stackFilter,
        ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters)
    {
        string eventIndex = ValidateIndexName(_configuration.Events.Name);
        string stackIndex = ValidateIndexName(_configuration.Stacks.Name);
        int bucketCount = Math.Clamp(request.BucketCount, 1, 100);
        string timeZone = FormatTimeZone(request.Offset);
        string utcStart = request.UtcStart.ToString("O", CultureInfo.InvariantCulture);
        string utcEnd = request.UtcEnd.ToString("O", CultureInfo.InvariantCulture);
        var query = new StringBuilder()
            .Append("SET time_zone = \"").Append(timeZone).Append("\"; FROM ").Append(eventIndex)
            .Append(" | KEEP stack_id, count, date, is_first_occurrence")
            .Append(" | LOOKUP JOIN ").Append(stackIndex)
            .Append(" ON stack_id == id AND is_deleted == false");

        if (!String.IsNullOrWhiteSpace(stackFilter))
        {
            query.Append(" AND QSTR(?stack_filter, {\"default_operator\": \"AND\"})");
            AddParameter(parameters, "stack_filter", FieldValue.String(stackFilter));
        }

        return query
            .Append(" | WHERE id IS NOT NULL")
            .Append(" | INLINE STATS total_documents = COUNT(*), total_events = SUM(COALESCE(count, 1)), total_stacks = COUNT_DISTINCT(stack_id, 40000), new_stacks = SUM(CASE(is_first_occurrence, 1, 0))")
            .Append(" | STATS documents = COUNT(*), events = SUM(COALESCE(count, 1)), stacks = COUNT_DISTINCT(stack_id), total_documents = MAX(total_documents), total_events = MAX(total_events), total_stacks = MAX(total_stacks), new_stacks = MAX(new_stacks)")
            .Append(" BY bucket = BUCKET(date, ").Append(bucketCount).Append(", \"").Append(utcStart).Append("\", \"").Append(utcEnd).Append("\")")
            .Append(" | SORT bucket | LIMIT ").Append(bucketCount + 2)
            .Append(" | KEEP bucket, documents, events, stacks, total_documents, total_events, total_stacks, new_stacks")
            .ToString();
    }

    private async Task<List<StackRollupEsqlRow>> ExecuteRowsAsync(
        string query,
        Query? sourceFilter,
        ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters,
        CancellationToken cancellationToken)
    {
        using var response = await _client.Esql.QueryAsync(new EsqlQueryRequest(query)
        {
            AllowPartialResults = false,
            Columnar = false,
            Filter = sourceFilter,
            Format = EsqlFormat.Json,
            Params = new Union<ICollection<ICollection<FieldValue>>, ICollection<KeyValuePair<string, ICollection<FieldValue>>>>(parameters)
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("Stack rollup lookup join failed with Elasticsearch status {StatusCode}", response.ApiCallDetails?.HttpStatusCode);
            throw new ApplicationException("The experimental stack rollup query failed.");
        }

        using var document = await JsonDocument.ParseAsync(response.Body, cancellationToken: cancellationToken);
        return ReadRows(document.RootElement);
    }

    private async Task<long> ExecuteTotalAsync(
        string query,
        Query? sourceFilter,
        ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters,
        CancellationToken cancellationToken)
    {
        using var response = await _client.Esql.QueryAsync(new EsqlQueryRequest(query)
        {
            AllowPartialResults = false,
            Columnar = false,
            Filter = sourceFilter,
            Format = EsqlFormat.Json,
            Params = new Union<ICollection<ICollection<FieldValue>>, ICollection<KeyValuePair<string, ICollection<FieldValue>>>>(parameters)
        }, cancellationToken);

        if (!response.IsValidResponse)
            throw new ApplicationException("The experimental stack rollup count query failed.");

        using var document = await JsonDocument.ParseAsync(response.Body, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("values", out var values) || values.GetArrayLength() == 0)
            return 0;

        return values[0][0].GetInt64();
    }

    private static List<StackRollupEsqlRow> ReadRows(JsonElement root)
    {
        if (!root.TryGetProperty("columns", out var columns) || !root.TryGetProperty("values", out var values))
            throw new JsonException("The ES|QL stack rollup response did not contain columns and values.");

        var columnIndexes = columns.EnumerateArray()
            .Select((column, index) => (Name: column.GetProperty("name").GetString(), Index: index))
            .Where(column => column.Name is not null)
            .ToDictionary(column => column.Name!, column => column.Index, StringComparer.Ordinal);

        int stackIdIndex = columnIndexes["stack_id"];
        int totalIndex = columnIndexes["event_total"];
        int usersIndex = columnIndexes["event_users"];
        int firstIndex = columnIndexes["event_first"];
        int lastIndex = columnIndexes["event_last"];
        int? totalStacksIndex = columnIndexes.TryGetValue("total_stacks", out int index) ? index : null;
        var rows = new List<StackRollupEsqlRow>();
        foreach (var value in values.EnumerateArray())
        {
            rows.Add(new StackRollupEsqlRow(
                value[stackIdIndex].GetString() ?? throw new JsonException("A stack rollup row did not contain a stack id."),
                value[totalIndex].GetInt64(),
                value[usersIndex].GetInt64(),
                value[firstIndex].GetDateTime().ToUniversalTime(),
                value[lastIndex].GetDateTime().ToUniversalTime(),
                totalStacksIndex.HasValue ? value[totalStacksIndex.Value].GetInt64() : null));
        }

        return rows;
    }

    private static StackRollupStatsResult ReadStats(JsonElement root)
    {
        if (!root.TryGetProperty("columns", out var columns) || !root.TryGetProperty("values", out var values))
            throw new JsonException("The ES|QL stack rollup stats response did not contain columns and values.");
        if (values.GetArrayLength() == 0)
            return new StackRollupStatsResult(0, 0, 0, []);

        var columnIndexes = GetColumnIndexes(columns);
        int bucketIndex = columnIndexes["bucket"];
        int documentsIndex = columnIndexes["documents"];
        int eventsIndex = columnIndexes["events"];
        int stacksIndex = columnIndexes["stacks"];
        int totalDocumentsIndex = columnIndexes["total_documents"];
        int totalEventsIndex = columnIndexes["total_events"];
        int totalStacksIndex = columnIndexes["total_stacks"];
        int newStacksIndex = columnIndexes["new_stacks"];
        var buckets = new List<StackRollupStatsBucket>(values.GetArrayLength());
        long totalEvents = 0;
        long totalStacks = 0;
        long newStacks = 0;
        long totalDocuments = 0;
        foreach (var value in values.EnumerateArray())
        {
            totalDocuments = value[totalDocumentsIndex].GetInt64();
            totalEvents = value[totalEventsIndex].GetInt64();
            totalStacks = value[totalStacksIndex].GetInt64();
            newStacks = value[newStacksIndex].GetInt64();
            buckets.Add(new StackRollupStatsBucket(
                value[bucketIndex].GetDateTimeOffset().UtcDateTime,
                value[eventsIndex].GetInt64(),
                value[stacksIndex].GetInt64(),
                value[documentsIndex].GetInt64()));
        }

        return new StackRollupStatsResult(totalEvents, totalStacks, newStacks, buckets, totalDocuments);
    }

    private static IReadOnlyDictionary<string, long> ReadProjectUserCounts(JsonElement root)
    {
        if (!root.TryGetProperty("columns", out var columns) || !root.TryGetProperty("values", out var values))
            throw new JsonException("The ES|QL project user response did not contain columns and values.");

        var columnIndexes = GetColumnIndexes(columns);
        int projectIndex = columnIndexes["project_id"];
        int usersIndex = columnIndexes["users"];
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var value in values.EnumerateArray())
        {
            string projectId = value[projectIndex].GetString() ?? throw new JsonException("A project user row did not contain a project id.");
            result[projectId] = value[usersIndex].GetInt64();
        }

        return result;
    }

    private static IReadOnlyDictionary<string, int> GetColumnIndexes(JsonElement columns)
        => columns.EnumerateArray()
            .Select((column, index) => (Name: column.GetProperty("name").GetString(), Index: index))
            .Where(column => column.Name is not null)
            .ToDictionary(column => column.Name!, column => column.Index, StringComparer.Ordinal);

    private static bool IsDashboardAggregation(string aggregations)
    {
        string value = aggregations.Replace(" ", String.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return value.Contains("date:(date", StringComparison.Ordinal)
            && value.Contains("cardinality:stack", StringComparison.Ordinal)
            && value.Contains("sum:count~1", StringComparison.Ordinal)
            && value.Contains("terms:(first@include:true)", StringComparison.Ordinal);
    }

    private static bool IsTermsTagsAggregation(string aggregations)
        => String.Equals(aggregations.Replace(" ", String.Empty, StringComparison.Ordinal), "terms:tags", StringComparison.OrdinalIgnoreCase);

    private static CountResult ToCountResult(StackRollupStatsResult stats)
    {
        static ValueAggregate Metric(double value) => new()
        {
            Value = value,
            Data = new Dictionary<string, object> { ["@type"] = "value" }
        };

        var dateBuckets = stats.Buckets.Select(bucket => (IBucket)new DateHistogramBucket(bucket.Date, new Dictionary<string, IAggregate>
        {
            ["cardinality_stack"] = Metric(bucket.Stacks),
            ["sum_count"] = Metric(bucket.Events)
        })
        {
            Key = new DateTimeOffset(bucket.Date).ToUnixTimeMilliseconds(),
            KeyAsString = bucket.Date.ToString("O", CultureInfo.InvariantCulture),
            Total = bucket.Documents,
            Data = new Dictionary<string, object> { ["@type"] = "datehistogram" }
        }).ToArray();

        var firstBuckets = new IBucket[]
        {
            new KeyedBucket<bool>(null)
            {
                Key = true,
                Total = stats.NewStacks,
                Data = new Dictionary<string, object> { ["@type"] = "bool" }
            }
        };

        return new CountResult(stats.Documents, new Dictionary<string, IAggregate>
        {
            ["date_date"] = new BucketAggregate
            {
                Items = dateBuckets,
                Data = new Dictionary<string, object> { ["@type"] = "bucket" }
            },
            ["cardinality_stack"] = Metric(stats.TotalStacks),
            ["terms_first"] = new BucketAggregate
            {
                Items = firstBuckets,
                Data = new Dictionary<string, object> { ["@type"] = "bucket" }
            },
            ["sum_count"] = Metric(stats.TotalEvents)
        });
    }

    private async Task<StackRollupReadiness> GetReadinessAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_readiness is not null && now < _readinessExpiresUtc)
            return _readiness;

        await _readinessLock.WaitAsync(cancellationToken);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_readiness is not null && now < _readinessExpiresUtc)
                return _readiness;

            _readiness = await CheckReadinessAsync(cancellationToken);
            _readinessExpiresUtc = now.Add(ReadinessCacheDuration);
            return _readiness;
        }
        finally
        {
            _readinessLock.Release();
        }
    }

    private async Task<StackRollupReadiness> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        var info = await _client.InfoAsync(cancellationToken);
        if (!info.IsValidResponse || !System.Version.TryParse(info.Version.Number, out var version) || version < new System.Version(9, 5))
            return new StackRollupReadiness(false, "elasticsearch-version");

        var settings = await _client.Indices.GetSettingsAsync((Indices)_configuration.Stacks.Name, cancellationToken);
        if (!settings.IsValidResponse || settings.Settings.Count != 1)
            return new StackRollupReadiness(false, "stack-alias-target");

        var indexSettings = settings.Settings.Single().Value.Settings?.Index;
        if (indexSettings is null || !String.Equals(indexSettings.Mode, "lookup", StringComparison.OrdinalIgnoreCase))
            return new StackRollupReadiness(false, "stack-index-mode");

        int shards = indexSettings.NumberOfShards is null
            ? 0
            : indexSettings.NumberOfShards.Match(value => value, value => Int32.TryParse(value, out int parsed) ? parsed : 0);
        return shards == 1
            ? new StackRollupReadiness(true, "ready")
            : new StackRollupReadiness(false, "stack-primary-shards");
    }

    private string EncodeCursor(StackRollupEsqlRow row, StackRollupSort sort, DateTime utcStart, DateTime utcEnd, string fingerprint)
    {
        long metric = sort.Metric switch
        {
            "event_total" => row.Total,
            "event_users" => row.Users,
            "event_first" => row.FirstOccurrence.Ticks,
            "event_last" => row.LastOccurrence.Ticks,
            _ => throw new InvalidOperationException("Unsupported stack rollup metric.")
        };
        var cursor = new StackRollupCursor(CursorVersion, sort.Value, metric, row.StackId, utcStart.Ticks, utcEnd.Ticks, fingerprint);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, _serializerOptions);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string EncodeEventCursor(EventLookupEsqlRow row, EventLookupSort sort, DateTime utcStart, DateTime utcEnd, string fingerprint)
    {
        var cursor = new EventLookupCursor(CursorVersion, sort.Value, row.Date.Ticks, row.EventId, utcStart.Ticks, utcEnd.Ticks, fingerprint);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, _serializerOptions);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private EventLookupCursor? DecodeEventCursor(string? token, string sort, string fingerprint)
    {
        if (String.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            string base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var cursor = JsonSerializer.Deserialize<EventLookupCursor>(Convert.FromBase64String(base64), _serializerOptions);
            if (cursor is null
                || cursor.Version != CursorVersion
                || !String.Equals(cursor.Sort, sort, StringComparison.Ordinal)
                || !String.Equals(cursor.Fingerprint, fingerprint, StringComparison.Ordinal)
                || String.IsNullOrWhiteSpace(cursor.EventId)
                || cursor.Date < DateTime.MinValue.Ticks
                || cursor.Date > DateTime.MaxValue.Ticks
                || cursor.UtcStart < DateTime.MinValue.Ticks
                || cursor.UtcStart > DateTime.MaxValue.Ticks
                || cursor.UtcEnd < DateTime.MinValue.Ticks
                || cursor.UtcEnd > DateTime.MaxValue.Ticks
                || cursor.UtcStart > cursor.UtcEnd)
            {
                throw new InvalidEventLookupCursorException("The event pagination cursor is not valid for this query.");
            }

            return cursor;
        }
        catch (InvalidEventLookupCursorException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or OverflowException)
        {
            throw new InvalidEventLookupCursorException("The event pagination cursor is malformed.");
        }
    }

    private StackRollupCursor? DecodeCursor(string? token, string sort, string fingerprint)
    {
        if (String.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            string base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var cursor = JsonSerializer.Deserialize<StackRollupCursor>(Convert.FromBase64String(base64), _serializerOptions);
            if (cursor is null
                || cursor.Version != CursorVersion
                || !String.Equals(cursor.Sort, sort, StringComparison.Ordinal)
                || !String.Equals(cursor.Fingerprint, fingerprint, StringComparison.Ordinal)
                || String.IsNullOrWhiteSpace(cursor.StackId)
                || cursor.UtcStart < DateTime.MinValue.Ticks
                || cursor.UtcStart > DateTime.MaxValue.Ticks
                || cursor.UtcEnd < DateTime.MinValue.Ticks
                || cursor.UtcEnd > DateTime.MaxValue.Ticks
                || cursor.UtcStart > cursor.UtcEnd
                || GetSort(sort).IsDate && (cursor.Metric < DateTime.MinValue.Ticks || cursor.Metric > DateTime.MaxValue.Ticks))
            {
                throw new InvalidStackRollupCursorException("The stack pagination cursor is not valid for this query.");
            }

            return cursor;
        }
        catch (InvalidStackRollupCursorException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or OverflowException)
        {
            throw new InvalidStackRollupCursorException("The stack pagination cursor is malformed.");
        }
    }

    private static string CreateFingerprint(StackRollupSearchRequest request)
    {
        string organizations = String.Join(',', request.AppFilter?.Organizations.Select(organization => organization.Id).Order(StringComparer.Ordinal) ?? Enumerable.Empty<string>());
        string projects = String.Join(',', request.AppFilter?.Projects?.Select(project => project.Id).Order(StringComparer.Ordinal) ?? Enumerable.Empty<string>());
        string value = String.Join('\n', [
            GetSort(request.Sort).Value,
            request.Filter ?? String.Empty,
            request.TimeExpression ?? String.Empty,
            request.Offset.Ticks.ToString(CultureInfo.InvariantCulture),
            organizations,
            projects,
            request.AppFilter?.Stack?.Id ?? String.Empty
        ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string CreateEventFingerprint(EventLookupSearchRequest request, string sort)
    {
        string organizations = String.Join(',', request.AppFilter?.Organizations.Select(organization => organization.Id).Order(StringComparer.Ordinal) ?? Enumerable.Empty<string>());
        string projects = String.Join(',', request.AppFilter?.Projects?.Select(project => project.Id).Order(StringComparer.Ordinal) ?? Enumerable.Empty<string>());
        string value = String.Join('\n', [
            sort,
            request.Filter ?? String.Empty,
            request.TimeExpression ?? String.Empty,
            organizations,
            projects,
            request.AppFilter?.Stack?.Id ?? String.Empty
        ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static StackRollupRow ToPublicRow(StackRollupEsqlRow row) => new(
        row.StackId,
        row.Total,
        row.Users,
        row.FirstOccurrence,
        row.LastOccurrence);

    private static StackRollupSort GetSort(string? sort) => (String.IsNullOrWhiteSpace(sort) ? "-total" : sort.Trim()) switch
    {
        "total" => new StackRollupSort("total", "event_total", false, true),
        "-total" => new StackRollupSort("-total", "event_total", false, false),
        "users" => new StackRollupSort("users", "event_users", false, true),
        "-users" => new StackRollupSort("-users", "event_users", false, false),
        "first_occurrence" => new StackRollupSort("first_occurrence", "event_first", true, true),
        "-first_occurrence" => new StackRollupSort("-first_occurrence", "event_first", true, false),
        "last_occurrence" => new StackRollupSort("last_occurrence", "event_last", true, true),
        "-last_occurrence" => new StackRollupSort("-last_occurrence", "event_last", true, false),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unsupported stack rollup sort.")
    };

    private static EventLookupSort GetEventSort(string? sort) => (String.IsNullOrWhiteSpace(sort) ? "-date" : sort.Trim()) switch
    {
        "date" => new EventLookupSort("date", true),
        "-date" => new EventLookupSort("-date", false),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unsupported event lookup sort.")
    };

    private static string StripAlternateInversion(string? filter) => filter?.StartsWith("@!", StringComparison.Ordinal) == true ? filter[2..] : filter ?? String.Empty;

    private static string ValidateIndexName(string index)
    {
        if (String.IsNullOrWhiteSpace(index) || index.Any(character => !Char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            throw new InvalidOperationException("The configured Elasticsearch index alias cannot be used in ES|QL.");

        return index;
    }

    private static string EscapeIdentifier(string field) => $"`{field.Replace("`", "``", StringComparison.Ordinal)}`";

    private static string FormatTimeZone(TimeSpan offset)
    {
        if (offset < TimeSpan.FromHours(-14) || offset > TimeSpan.FromHours(14))
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The stack rollup time zone offset must be between -14:00 and +14:00.");

        string sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return $"{sign}{(int)absolute.TotalHours:00}:{absolute.Minutes:00}";
    }

    private static void AddParameter(ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters, string name, FieldValue value)
        => parameters.Add(new KeyValuePair<string, ICollection<FieldValue>>(name, [value]));

    private static void AddParameter(ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters, string name, ICollection<FieldValue> values)
        => parameters.Add(new KeyValuePair<string, ICollection<FieldValue>>(name, values));

    private sealed record StackRollupSort(string Value, string Metric, bool IsDate, bool Ascending);
    private sealed record EventLookupSort(string Value, bool Ascending);
    private sealed record StackRollupReadiness(bool IsReady, string Reason);
    private sealed record StackRollupCursor(int Version, string Sort, long Metric, string StackId, long UtcStart, long UtcEnd, string Fingerprint);
    private sealed record EventLookupCursor(int Version, string Sort, long Date, string EventId, long UtcStart, long UtcEnd, string Fingerprint);
    private sealed record EventLookupEsqlRow(string EventId, DateTime Date, long? TotalEvents);
    private sealed record StackRollupEsqlRow(
        string StackId,
        long Total,
        long Users,
        DateTime FirstOccurrence,
        DateTime LastOccurrence,
        long? TotalStacks);
}
