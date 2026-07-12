using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Seed;

public class PredefinedSavedViewsDataSeed : IDataSeed
{
    public const string SystemOrganizationId = "000000000000000000000001";
    public const string SystemUserId = "000000000000000000000001";
    public const string SeedFileName = "predefined-saved-views.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ILockProvider _lockProvider;
    private readonly ILogger _logger;

    public PredefinedSavedViewsDataSeed(ISavedViewRepository savedViewRepository, ILockProvider lockProvider, ILoggerFactory loggerFactory)
    {
        _savedViewRepository = savedViewRepository;
        _lockProvider = lockProvider;
        _logger = loggerFactory.CreateLogger<PredefinedSavedViewsDataSeed>();
    }

    public string Name => "Predefined Saved Views";

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.TryUsingAsync("data-seed:predefined-saved-views", async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definitions = await ReadDefaultSavedViewsAsync(cancellationToken);
            var existingResults = await _savedViewRepository.GetByOrganizationIdAsync(SystemOrganizationId, o => o.PageLimit(1000));

            if (existingResults.Total == 0)
            {
                // First-time seed: create all views from definitions.
                var savedViews = definitions.Select(CreateSavedView).ToList();
                await _savedViewRepository.AddAsync(savedViews, o => o.Cache().ImmediateConsistency());
                _logger.LogInformation("Seeded {Count} predefined saved views", savedViews.Count);
                return;
            }

            // Update existing views whose fields have drifted from the definitions.
            // Never re-create views that were manually deleted at runtime.
            var existingByKey = existingResults.Documents
                .Where(v => !String.IsNullOrEmpty(v.PredefinedKey))
                .ToDictionary(v => v.PredefinedKey!, StringComparer.OrdinalIgnoreCase);

            var toSave = new List<SavedView>();
            foreach (var definition in definitions)
            {
                if (!existingByKey.TryGetValue(definition.Key, out var existing))
                    continue;

                if (ApplyDefinition(existing, definition))
                    toSave.Add(existing);
            }

            if (toSave.Count > 0)
            {
                await _savedViewRepository.SaveAsync(toSave, o => o.Cache().ImmediateConsistency());
                _logger.LogInformation("Updated {Count} stale predefined saved views", toSave.Count);
            }
        }, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
    }

    private static bool ApplyDefinition(SavedView existing, PredefinedSavedViewDefinition definition)
    {
        bool changed = false;

        if (!String.Equals(existing.PredefinedKey, definition.Key, StringComparison.Ordinal))
        {
            existing.PredefinedKey = definition.Key;
            changed = true;
        }

        if (!String.Equals(existing.ViewType, definition.ViewType, StringComparison.Ordinal))
        {
            existing.ViewType = definition.ViewType;
            changed = true;
        }

        if (!String.Equals(existing.Name, definition.Name, StringComparison.Ordinal))
        {
            existing.Name = definition.Name;
            changed = true;
        }

        if (!String.Equals(existing.Slug, definition.Slug, StringComparison.Ordinal))
        {
            existing.Slug = definition.Slug;
            changed = true;
        }

        if (!String.Equals(existing.Filter, definition.Filter, StringComparison.Ordinal))
        {
            existing.Filter = definition.Filter;
            changed = true;
        }

        if (!String.Equals(existing.Time, definition.Time, StringComparison.Ordinal))
        {
            existing.Time = definition.Time;
            changed = true;
        }

        if (!String.Equals(existing.Sort, definition.Sort, StringComparison.Ordinal))
        {
            existing.Sort = definition.Sort;
            changed = true;
        }

        string? filterDefinitions = GetRawJson(definition.FilterDefinitions);
        if (!String.Equals(existing.FilterDefinitions, filterDefinitions, StringComparison.Ordinal))
        {
            existing.FilterDefinitions = filterDefinitions;
            changed = true;
        }

        if (!DictionaryEquals(existing.Columns, definition.Columns))
        {
            existing.Columns = definition.Columns?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            changed = true;
        }

        if (!CollectionEquals(existing.ColumnOrder, definition.ColumnOrder))
        {
            existing.ColumnOrder = definition.ColumnOrder is null ? null : [.. definition.ColumnOrder];
            changed = true;
        }

        if (existing.ShowStats != definition.ShowStats)
        {
            existing.ShowStats = definition.ShowStats;
            changed = true;
        }

        if (existing.ShowChart != definition.ShowChart)
        {
            existing.ShowChart = definition.ShowChart;
            changed = true;
        }

        string contentHash = PredefinedSavedViewContentHasher.GetContentHash(existing);
        if (!String.Equals(existing.PredefinedContentHash, contentHash, StringComparison.Ordinal))
        {
            existing.PredefinedContentHash = contentHash;
            changed = true;
        }

        return changed;
    }

    public static async Task<IReadOnlyCollection<PredefinedSavedViewDefinition>> ReadDefaultSavedViewsAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(GetSeedFilePath());
        var definitions = await JsonSerializer.DeserializeAsync<List<PredefinedSavedViewDefinition>>(stream, JsonOptions, cancellationToken);
        return definitions ?? [];
    }

    private static string GetSeedFilePath()
    {
        string seedFileName = Path.GetFileName(SeedFileName);
        return Path.Combine(AppContext.BaseDirectory, "Seed", seedFileName);
    }

    private static SavedView CreateSavedView(PredefinedSavedViewDefinition definition)
    {
        var savedView = new SavedView
        {
            OrganizationId = SystemOrganizationId,
            CreatedByUserId = SystemUserId,
            PredefinedKey = definition.Key,
            Name = definition.Name,
            Slug = definition.Slug,
            ViewType = definition.ViewType,
            Filter = definition.Filter,
            Time = definition.Time,
            Sort = definition.Sort,
            FilterDefinitions = GetRawJson(definition.FilterDefinitions),
            Columns = definition.Columns?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ColumnOrder = definition.ColumnOrder is null ? null : [.. definition.ColumnOrder],
            ShowStats = definition.ShowStats,
            ShowChart = definition.ShowChart,
            Version = 1
        };

        savedView.PredefinedContentHash = PredefinedSavedViewContentHasher.GetContentHash(savedView);
        return savedView;
    }

    private static bool CollectionEquals(IReadOnlyCollection<string>? left, IReadOnlyCollection<string>? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null || left.Count != right.Count)
            return false;

        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool DictionaryEquals(IReadOnlyDictionary<string, bool>? left, IReadOnlyDictionary<string, bool>? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null || left.Count != right.Count)
            return false;

        return left.All(kvp => right.TryGetValue(kvp.Key, out bool value) && value == kvp.Value);
    }

    public static string? GetRawJson(JsonElement? value)
    {
        if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return element.GetRawText();
    }
}

public sealed record PredefinedSavedViewDefinition
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("viewType")]
    public required string ViewType { get; init; }

    [JsonPropertyName("filter")]
    public string? Filter { get; init; }

    [JsonPropertyName("time")]
    public string? Time { get; init; }

    [JsonPropertyName("sort")]
    public string? Sort { get; init; }

    [JsonPropertyName("filterDefinitions")]
    public JsonElement? FilterDefinitions { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyDictionary<string, bool>? Columns { get; init; }

    [JsonPropertyName("columnOrder")]
    public IReadOnlyCollection<string>? ColumnOrder { get; init; }

    [JsonPropertyName("showStats")]
    public bool? ShowStats { get; init; }

    [JsonPropertyName("showChart")]
    public bool? ShowChart { get; init; }
}
