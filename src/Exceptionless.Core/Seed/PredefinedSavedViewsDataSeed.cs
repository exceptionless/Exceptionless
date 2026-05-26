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

            if (await _savedViewRepository.CountByOrganizationIdAsync(SystemOrganizationId) > 0)
                return;

            var definitions = await ReadDefaultSavedViewsAsync(cancellationToken);
            var savedViews = definitions.Select(CreateSavedView).ToList();
            await _savedViewRepository.AddAsync(savedViews, o => o.Cache().ImmediateConsistency());
            _logger.LogInformation("Seeded {Count} predefined saved views", savedViews.Count);
        }, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
    }

    public static async Task<IReadOnlyCollection<PredefinedSavedViewDefinition>> ReadDefaultSavedViewsAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(GetSeedFilePath());
        var definitions = await JsonSerializer.DeserializeAsync<List<PredefinedSavedViewDefinition>>(stream, JsonOptions, cancellationToken);
        return definitions ?? [];
    }

    private static string GetSeedFilePath()
    {
        var seedFileName = Path.GetFileName(SeedFileName);
        return Path.Combine(AppContext.BaseDirectory, "Seed", seedFileName);
    }

    private static SavedView CreateSavedView(PredefinedSavedViewDefinition definition)
    {
        return new SavedView
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