namespace Exceptionless.Core.Models;

/// <summary>
/// Per-column display settings for a saved view. All properties are optional so new settings can
/// be added without changing the meaning of existing saved views.
/// </summary>
public sealed record SavedViewColumnSettings
{
    public const int MaxPosition = 49;
    public const int MaxWidth = 1200;
    public const int MinWidth = 48;

    /// <summary>Whether the column is visible. Null means use the table default.</summary>
    public bool? Visible { get; set; }

    /// <summary>Zero-based display position. Null means use the table default order.</summary>
    public int? Position { get; set; }

    /// <summary>Column width in pixels. Null means use the table default width.</summary>
    public int? Width { get; set; }

    public static Dictionary<string, SavedViewColumnSettings>? FromLegacy(
        IReadOnlyDictionary<string, bool>? columns,
        IReadOnlyCollection<string>? columnOrder)
    {
        return MergeLegacy(null, columns, columnOrder, columns is not null, columnOrder is not null);
    }

    public static Dictionary<string, SavedViewColumnSettings>? MergeLegacy(
        IReadOnlyDictionary<string, SavedViewColumnSettings>? current,
        IReadOnlyDictionary<string, bool>? columns,
        IReadOnlyCollection<string>? columnOrder,
        bool replaceVisibility,
        bool replaceOrder)
    {
        var result = current?.ToDictionary(entry => entry.Key, entry => entry.Value with { })
            ?? new Dictionary<string, SavedViewColumnSettings>();

        if (replaceVisibility)
        {
            foreach (var settings in result.Values)
                settings.Visible = null;

            if (columns is not null)
            {
                foreach (var (columnId, visible) in columns)
                    GetOrCreate(result, columnId).Visible = visible;
            }
        }

        if (replaceOrder)
        {
            foreach (var settings in result.Values)
                settings.Position = null;

            int position = 0;
            if (columnOrder is not null)
            {
                foreach (string columnId in columnOrder)
                    GetOrCreate(result, columnId).Position = position++;
            }
        }

        foreach (string columnId in result.Where(entry => IsEmpty(entry.Value)).Select(entry => entry.Key).ToArray())
            result.Remove(columnId);

        return result.Count > 0 ? result : null;
    }

    public static List<string>? ToLegacyColumnOrder(IReadOnlyDictionary<string, SavedViewColumnSettings>? columnSettings)
    {
        var result = columnSettings?
            .Where(entry => entry.Value.Position.HasValue)
            .OrderBy(entry => entry.Value.Position)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToList();

        return result is { Count: > 0 } ? result : null;
    }

    public static Dictionary<string, bool>? ToLegacyColumns(IReadOnlyDictionary<string, SavedViewColumnSettings>? columnSettings)
    {
        var result = columnSettings?
            .Where(entry => entry.Value.Visible.HasValue)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Visible!.Value);

        return result is { Count: > 0 } ? result : null;
    }

    private static SavedViewColumnSettings GetOrCreate(IDictionary<string, SavedViewColumnSettings> settings, string columnId)
    {
        if (settings.TryGetValue(columnId, out var existing))
            return existing;

        var created = new SavedViewColumnSettings();
        settings[columnId] = created;
        return created;
    }

    private static bool IsEmpty(SavedViewColumnSettings settings)
        => settings.Visible is null && settings.Position is null && settings.Width is null;
}
