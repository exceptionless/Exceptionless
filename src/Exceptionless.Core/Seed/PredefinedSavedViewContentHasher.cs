using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Seed;

public static class PredefinedSavedViewContentHasher
{
    public static string GetContentHash(SavedView savedView)
    {
        ArgumentNullException.ThrowIfNull(savedView);

        return GetContentHash(
            savedView.Name,
            savedView.Slug,
            savedView.ViewType,
            savedView.Filter,
            savedView.Time,
            savedView.Sort,
            savedView.FilterDefinitions,
            savedView.Columns,
            savedView.ColumnOrder,
            savedView.ShowStats,
            savedView.ShowChart);
    }

    public static string GetDefinitionsContentHash(IEnumerable<PredefinedSavedViewDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var content = definitions
            .OrderBy(definition => definition.Key, StringComparer.Ordinal)
            .Select(definition => new
            {
                definition.Key,
                Hash = GetContentHash(
                    definition.Name,
                    definition.Slug,
                    definition.ViewType,
                    definition.Filter,
                    definition.Time,
                    definition.Sort,
                    PredefinedSavedViewsDataSeed.GetRawJson(definition.FilterDefinitions),
                    definition.Columns,
                    definition.ColumnOrder,
                    definition.ShowStats,
                    definition.ShowChart)
            });

        return SerializeAndHash(content);
    }

    private static string GetContentHash(
        string name,
        string slug,
        string viewType,
        string? filter,
        string? time,
        string? sort,
        string? filterDefinitions,
        IReadOnlyDictionary<string, bool>? columns,
        IReadOnlyCollection<string>? columnOrder,
        bool? showStats,
        bool? showChart)
    {
        var content = new
        {
            name,
            slug,
            viewType,
            filter,
            time,
            sort,
            filterDefinitions,
            Columns = columns?.OrderBy(column => column.Key, StringComparer.Ordinal),
            columnOrder,
            showStats,
            showChart
        };

        return SerializeAndHash(content);
    }

    private static string SerializeAndHash<T>(T content)
    {
        string json = JsonSerializer.Serialize(content);
        return json.Replace(" ", String.Empty, StringComparison.Ordinal).ToSHA256();
    }
}
