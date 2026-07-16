using Exceptionless.Core.Models;

namespace Exceptionless.Core.Seed;

public static class PredefinedSavedViewConfiguration
{
    public static bool Apply(SavedView destination, SavedView source, string key, string slug)
    {
        bool changed = false;
        changed |= SetIfChanged(destination, null, static (view, value) => view.UserId = value, static view => view.UserId);
        changed |= SetIfChanged(destination, key, static (view, value) => view.PredefinedKey = value, static view => view.PredefinedKey);
        changed |= SetIfChanged(destination, source.Name, static (view, value) => view.Name = value, static view => view.Name);
        changed |= SetIfChanged(destination, slug, static (view, value) => view.Slug = value, static view => view.Slug);
        changed |= SetIfChanged(destination, source.ViewType, static (view, value) => view.ViewType = value, static view => view.ViewType);
        changed |= SetIfChanged(destination, source.Filter, static (view, value) => view.Filter = value, static view => view.Filter);
        changed |= SetIfChanged(destination, source.Time, static (view, value) => view.Time = value, static view => view.Time);
        changed |= SetIfChanged(destination, source.Sort, static (view, value) => view.Sort = value, static view => view.Sort);
        changed |= SetIfChanged(destination, source.FilterDefinitions, static (view, value) => view.FilterDefinitions = value, static view => view.FilterDefinitions);
        changed |= SetDictionaryIfChanged(destination, source.Columns);
        changed |= SetListIfChanged(destination, source.ColumnOrder);
        changed |= SetIfChanged(destination, source.ShowStats, static (view, value) => view.ShowStats = value, static view => view.ShowStats);
        changed |= SetIfChanged(destination, source.ShowChart, static (view, value) => view.ShowChart = value, static view => view.ShowChart);
        changed |= SetIfChanged(destination, source.Version, static (view, value) => view.Version = value, static view => view.Version);
        changed |= SetIfChanged(destination, PredefinedSavedViewContentHasher.GetContentHash(destination), static (view, value) => view.PredefinedContentHash = value, static view => view.PredefinedContentHash);

        return changed;
    }

    private static bool SetDictionaryIfChanged(SavedView savedView, IReadOnlyDictionary<string, bool>? value)
    {
        if (DictionaryEquals(savedView.Columns, value))
            return false;

        savedView.Columns = value is null ? null : new Dictionary<string, bool>(value);
        return true;
    }

    private static bool SetIfChanged<T>(SavedView savedView, T value, Action<SavedView, T> setter, Func<SavedView, T> getter)
    {
        if (EqualityComparer<T>.Default.Equals(getter(savedView), value))
            return false;

        setter(savedView, value);
        return true;
    }

    private static bool SetListIfChanged(SavedView savedView, IReadOnlyCollection<string>? value)
    {
        if (CollectionEquals(savedView.ColumnOrder, value))
            return false;

        savedView.ColumnOrder = value is null ? null : [.. value];
        return true;
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
}
