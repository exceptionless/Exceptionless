using System.Text.Json;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Seed;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class MigrateSavedViewColumns : MigrationBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ElasticsearchClient _client;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly ITextSerializer _serializer;

    public MigrateSavedViewColumns(
        ExceptionlessElasticConfiguration configuration,
        ITextSerializer serializer,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _client = configuration.Client;
        _configuration = configuration;
        _serializer = serializer;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 5;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        const int pageSize = 500;
        var pointInTime = await _client.OpenPointInTimeAsync(
            _configuration.SavedViews.VersionedName,
            request => request.KeepAlive("2m"),
            context.CancellationToken);
        _logger.LogRequest(pointInTime);

        if (!pointInTime.IsValidResponse || String.IsNullOrWhiteSpace(pointInTime.Id))
            throw new InvalidOperationException("Unable to open a point in time for the saved view migration.");

        string pointInTimeId = pointInTime.Id;
        FieldValue[]? searchAfter = null;
        int migrated = 0;

        try
        {
            do
            {
                var response = await _client.SearchAsync<JsonElement>(request =>
                {
                    request
                        .Pit(pit => pit.Id(pointInTimeId).KeepAlive("2m"))
                        .SeqNoPrimaryTerm()
                        .Size(pageSize)
                        .Sort(sort => sort.Field("_shard_doc"));

                    if (searchAfter is not null)
                        request.SearchAfter(searchAfter);
                }, context.CancellationToken);
                _logger.LogRequest(response);

                if (!response.IsValidResponse)
                    throw new InvalidOperationException("Unable to read saved views during the column migration.");

                if (!String.IsNullOrWhiteSpace(response.PitId))
                    pointInTimeId = response.PitId;

                foreach (var hit in response.Hits)
                {
                    if (hit.Source.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                        continue;

                    var source = JsonNode.Parse(hit.Source.GetRawText())?.AsObject();
                    if (source is null)
                        continue;

                    if (hit.SeqNo is null || hit.PrimaryTerm is null)
                        throw new InvalidOperationException($"Unable to read concurrency metadata for saved view '{hit.Id}'.");

                    string? legacyContentHash = GetLegacyContentHash(source);
                    bool columnsMigrated = TryMigrate(source);
                    bool contentHashMigrated = UpdatePredefinedContentHash(source, legacyContentHash);
                    if (!columnsMigrated && !contentHashMigrated)
                        continue;

                    var indexResponse = await IndexMigratedDocumentAsync(
                        source,
                        hit.Id,
                        hit.SeqNo.Value,
                        hit.PrimaryTerm.Value,
                        context.CancellationToken);
                    _logger.LogRequest(indexResponse);

                    if (!indexResponse.IsValidResponse)
                    {
                        string reason = indexResponse.ApiCallDetails.HttpStatusCode == 409
                            ? " because it changed while the migration was running"
                            : String.Empty;
                        throw new InvalidOperationException($"Unable to migrate saved view '{hit.Id}'{reason}. The resumable migration can be run again safely.");
                    }

                    migrated++;
                }

                searchAfter = response.Hits.Count == pageSize
                    ? response.Hits.Last().Sort!.ToArray()
                    : null;

                await context.Lock.RenewAsync();
            } while (searchAfter is not null && !context.CancellationToken.IsCancellationRequested);
        }
        finally
        {
            var closeResponse = await _client.ClosePointInTimeAsync(
                request => request.Id(pointInTimeId),
                CancellationToken.None);
            _logger.LogRequest(closeResponse);
        }

        _logger.LogInformation("Migrated {SavedViewCount} saved view column configurations", migrated);
    }

    internal Task<IndexResponse> IndexMigratedDocumentAsync(
        JsonObject source,
        string id,
        long sequenceNumber,
        long primaryTerm,
        CancellationToken cancellationToken)
    {
        return _client.IndexAsync(
            source,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(id)
                .IfSeqNo(sequenceNumber)
                .IfPrimaryTerm(primaryTerm),
            cancellationToken);
    }

    internal static bool TryMigrate(JsonObject source)
    {
        var columns = source["columns"] as JsonObject;
        var columnOrder = source["column_order"] as JsonArray;
        bool hasLegacyColumns = columns?.Any(entry => entry.Value is JsonValue value && value.TryGetValue<bool>(out _)) == true;

        if (!hasLegacyColumns && columnOrder is null)
            return false;

        var migratedColumns = new JsonObject();
        if (columns is not null)
        {
            foreach (var (columnId, value) in columns)
            {
                if (value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out bool visible))
                {
                    migratedColumns[columnId] = new JsonObject
                    {
                        ["visible"] = visible
                    };
                }
                else if (value is JsonObject settings)
                {
                    migratedColumns[columnId] = settings.DeepClone();
                }
            }
        }

        if (columnOrder is not null)
        {
            for (int position = 0; position < columnOrder.Count; position++)
            {
                string? columnId = columnOrder[position]?.GetValue<string>();
                if (String.IsNullOrWhiteSpace(columnId))
                    continue;

                if (migratedColumns[columnId] is not JsonObject settings)
                {
                    settings = new JsonObject();
                    migratedColumns[columnId] = settings;
                }

                settings["position"] = position;
            }
        }

        source["columns"] = migratedColumns.Count > 0 ? migratedColumns : null;
        source.Remove("column_order");
        return true;
    }

    private bool UpdatePredefinedContentHash(JsonObject source, string? legacyContentHash)
    {
        string? predefinedContentHash = source["predefined_content_hash"]?.GetValue<string>();
        if (String.IsNullOrWhiteSpace(predefinedContentHash))
            return false;

        if (!String.Equals(predefinedContentHash, legacyContentHash, StringComparison.Ordinal))
            return false;

        var savedView = _serializer.Deserialize<SavedView>(source.ToJsonString(JsonOptions));
        if (savedView is null)
            throw new InvalidOperationException("Unable to deserialize a migrated saved view.");

        source["predefined_content_hash"] = PredefinedSavedViewContentHasher.GetContentHash(savedView);
        return true;
    }

    internal static string? GetLegacyContentHash(JsonObject source)
    {
        var columns = source["columns"] as JsonObject;
        if (columns?.Any(entry => entry.Value is not JsonValue value || !value.TryGetValue<bool>(out _)) == true)
            return null;

        var legacyColumns = columns?.ToDictionary(
            entry => entry.Key,
            entry => entry.Value!.GetValue<bool>());
        var columnOrder = (source["column_order"] as JsonArray)?
            .Select(value => value?.GetValue<string>())
            .Where(value => value is not null)
            .Cast<string>()
            .ToList();

        var content = new
        {
            name = source["name"]?.GetValue<string>(),
            slug = source["slug"]?.GetValue<string>(),
            viewType = source["view_type"]?.GetValue<string>(),
            filter = source["filter"]?.GetValue<string>(),
            time = source["time"]?.GetValue<string>(),
            sort = source["sort"]?.GetValue<string>(),
            filterDefinitions = CanonicalizeFilterDefinitions(source["filter_definitions"]?.GetValue<string>()),
            Columns = legacyColumns?.OrderBy(column => column.Key, StringComparer.Ordinal),
            columnOrder,
            showStats = source["show_stats"]?.GetValue<bool?>(),
            showChart = source["show_chart"]?.GetValue<bool?>()
        };

        return JsonSerializer.Serialize(content).ToSHA256();
    }

    private static string? CanonicalizeFilterDefinitions(string? filterDefinitions)
    {
        if (String.IsNullOrWhiteSpace(filterDefinitions))
            return filterDefinitions;

        try
        {
            return JsonNode.Parse(filterDefinitions)?.ToJsonString() ?? filterDefinitions;
        }
        catch (JsonException)
        {
            return filterDefinitions;
        }
    }
}
