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
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

public interface IStackRollupSearchService
{
    Task<StackRollupSearchResult> SearchAsync(StackRollupSearchRequest request, CancellationToken cancellationToken = default);
}

public sealed record StackRollupSearchRequest(
    AppFilter? AppFilter,
    DateTime UtcStart,
    DateTime UtcEnd,
    TimeSpan Offset,
    string? TimeExpression,
    string? Filter,
    string Mode,
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

public sealed record StackRollupRow(
    string StackId,
    long Total,
    long Users,
    DateTime FirstOccurrence,
    DateTime LastOccurrence);

public sealed class InvalidStackRollupCursorException(string message) : Exception(message);

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

    public async Task<StackRollupSearchResult> SearchAsync(StackRollupSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedMode(request.Mode))
            throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Unsupported stack rollup mode.");

        var readiness = await GetReadinessAsync(cancellationToken);
        if (!readiness.IsReady)
        {
            _logger.LogError("Stack rollup lookup join prerequisite failed: {Reason}", readiness.Reason);
            throw new InvalidOperationException($"The stack rollup lookup join prerequisite failed: {readiness.Reason}.");
        }

        string fingerprint = CreateFingerprint(request);
        StackRollupCursor? cursor = DecodeCursor(request.Before ?? request.After, request.Mode, fingerprint);
        DateTime utcStart = cursor is null ? request.UtcStart : new DateTime(cursor.UtcStart, DateTimeKind.Utc);
        DateTime utcEnd = cursor is null ? request.UtcEnd : new DateTime(cursor.UtcEnd, DateTimeKind.Utc);
        string normalizedFilter = StripAlternateInversion(request.Filter);
        if (request.Mode == "stack_new")
            normalizedFilter = AddFirstOccurrenceFilter(utcStart, utcEnd, normalizedFilter);
        string? eventFilter = await _eventStackFilter.GetEventFilterAsync(normalizedFilter);
        string? stackFilter = (await _eventStackFilter.GetStackFilterAsync(normalizedFilter))?.Filter;
        Query? sourceFilter = await BuildSourceFilterAsync(request.AppFilter, utcStart, utcEnd, eventFilter);

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Executing ES|QL stack rollup mode {Mode}, direction {Direction}, range {UtcStart:o} to {UtcEnd:o}, event filter {HasEventFilter}, stack filter {HasStackFilter}",
            request.Mode,
            request.Before is not null ? "before" : request.After is not null ? "after" : "initial",
            utcStart,
            utcEnd,
            !String.IsNullOrWhiteSpace(eventFilter),
            !String.IsNullOrWhiteSpace(stackFilter));

        var parameters = new List<KeyValuePair<string, ICollection<FieldValue>>>();
        string query = BuildQuery(request, cursor, stackFilter, parameters, countOnly: false);
        var rows = await ExecuteRowsAsync(query, sourceFilter, parameters, cancellationToken);
        long? total = request.IncludeTotal ? rows.FirstOrDefault()?.TotalStacks : null;

        if (request.IncludeTotal && total is null)
        {
            parameters.Clear();
            string countQuery = BuildQuery(request, cursor: null, stackFilter, parameters, countOnly: true);
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
        string? before = hasPrevious ? EncodeCursor(rows[0], request, utcStart, utcEnd, fingerprint) : null;
        string? after = hasNext ? EncodeCursor(rows[^1], request, utcStart, utcEnd, fingerprint) : null;

        _logger.LogDebug(
            "Completed ES|QL stack rollup mode {Mode}, direction {Direction}, rows {RowCount}, has more {HasMore}, duration {DurationMs}ms",
            request.Mode,
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

    private string BuildQuery(
        StackRollupSearchRequest request,
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

        var mode = GetMode(request.Mode);
        bool isBefore = request.Before is not null;
        if (cursor is not null)
        {
            string primaryComparison = isBefore ? ">" : "<";
            string idComparison = isBefore ? "<" : ">";
            string metricParameter = mode.IsDate ? "TO_DATETIME(?cursor_metric)" : "?cursor_metric";
            query
                .Append(" | WHERE ").Append(mode.Metric).Append(' ').Append(primaryComparison).Append(' ').Append(metricParameter)
                .Append(" OR (").Append(mode.Metric).Append(" == ").Append(metricParameter)
                .Append(" AND stack_id ").Append(idComparison).Append(" ?cursor_stack_id)");
            AddParameter(parameters, "cursor_metric", mode.IsDate
                ? FieldValue.String(new DateTime(cursor.Metric, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture))
                : FieldValue.Long(cursor.Metric));
            AddParameter(parameters, "cursor_stack_id", FieldValue.String(cursor.StackId));
        }

        string primarySort = isBefore ? "ASC" : "DESC";
        string idSort = isBefore ? "DESC" : "ASC";
        query
            .Append(" | SORT ").Append(mode.Metric).Append(' ').Append(primarySort).Append(", stack_id ").Append(idSort)
            .Append(" | LIMIT ").Append(request.Limit + 1)
            .Append(" | KEEP stack_id, event_total, event_users, event_first, event_last");

        if (request.IncludeTotal)
            query.Append(", total_stacks");

        return query.ToString();
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

    private string EncodeCursor(StackRollupEsqlRow row, StackRollupSearchRequest request, DateTime utcStart, DateTime utcEnd, string fingerprint)
    {
        var mode = GetMode(request.Mode);
        long metric = mode.Metric switch
        {
            "event_total" => row.Total,
            "event_users" => row.Users,
            "event_first" => row.FirstOccurrence.Ticks,
            "event_last" => row.LastOccurrence.Ticks,
            _ => throw new InvalidOperationException("Unsupported stack rollup metric.")
        };
        var cursor = new StackRollupCursor(CursorVersion, request.Mode, metric, row.StackId, utcStart.Ticks, utcEnd.Ticks, fingerprint);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, _serializerOptions);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private StackRollupCursor? DecodeCursor(string? token, string mode, string fingerprint)
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
                || !String.Equals(cursor.Mode, mode, StringComparison.Ordinal)
                || !String.Equals(cursor.Fingerprint, fingerprint, StringComparison.Ordinal)
                || String.IsNullOrWhiteSpace(cursor.StackId)
                || cursor.UtcStart < DateTime.MinValue.Ticks
                || cursor.UtcStart > DateTime.MaxValue.Ticks
                || cursor.UtcEnd < DateTime.MinValue.Ticks
                || cursor.UtcEnd > DateTime.MaxValue.Ticks
                || cursor.UtcStart > cursor.UtcEnd
                || GetMode(mode).IsDate && (cursor.Metric < DateTime.MinValue.Ticks || cursor.Metric > DateTime.MaxValue.Ticks))
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
            request.Mode,
            request.Filter ?? String.Empty,
            request.TimeExpression ?? String.Empty,
            request.Offset.Ticks.ToString(CultureInfo.InvariantCulture),
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

    private static StackRollupMode GetMode(string mode) => mode switch
    {
        "stack_recent" => new StackRollupMode("event_last", true),
        "stack_frequent" => new StackRollupMode("event_total", false),
        "stack_new" => new StackRollupMode("event_first", true),
        "stack_users" => new StackRollupMode("event_users", false),
        _ => throw new InvalidOperationException("Unsupported stack rollup mode.")
    };

    private static bool IsSupportedMode(string mode) => mode is "stack_recent" or "stack_frequent" or "stack_new" or "stack_users";

    private static string StripAlternateInversion(string? filter) => filter?.StartsWith("@!", StringComparison.Ordinal) == true ? filter[2..] : filter ?? String.Empty;

    private static string AddFirstOccurrenceFilter(DateTime utcStart, DateTime utcEnd, string? filter)
    {
        string range = $"first_occurrence:[\"{utcStart:O}\" TO \"{utcEnd:O}\"]";
        return String.IsNullOrWhiteSpace(filter) ? range : $"{range} ({filter})";
    }

    private static string ValidateIndexName(string index)
    {
        if (String.IsNullOrWhiteSpace(index) || index.Any(character => !Char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            throw new InvalidOperationException("The configured Elasticsearch index alias cannot be used in ES|QL.");

        return index;
    }

    private static string EscapeIdentifier(string field) => $"`{field.Replace("`", "``", StringComparison.Ordinal)}`";

    private static void AddParameter(ICollection<KeyValuePair<string, ICollection<FieldValue>>> parameters, string name, FieldValue value)
        => parameters.Add(new KeyValuePair<string, ICollection<FieldValue>>(name, [value]));

    private sealed record StackRollupMode(string Metric, bool IsDate);
    private sealed record StackRollupReadiness(bool IsReady, string Reason);
    private sealed record StackRollupCursor(int Version, string Mode, long Metric, string StackId, long UtcStart, long UtcEnd, string Fingerprint);
    private sealed record StackRollupEsqlRow(
        string StackId,
        long Total,
        long Users,
        DateTime FirstOccurrence,
        DateTime LastOccurrence,
        long? TotalStacks);
}
