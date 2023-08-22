using System.Collections.Concurrent;

namespace Exceptionless.Core.Extensions;

public static class DictionaryExtensions
{
    public static void Trim(this HashSet<string> items, Predicate<string> itemsToRemove, Predicate<string> itemsToAlwaysInclude, int maxLength)
    {
        items.RemoveWhere(itemsToRemove);
        if (maxLength > 0 && items.Count > maxLength)
        {
            foreach (string item in items.ToList())
            {
                if (items.Count <= maxLength)
                    break;

                if (itemsToAlwaysInclude(item))
                    continue;

                items.Remove(item);
            }
        }
    }

    public static void AddItemIfNotEmpty(this IDictionary<string, string> dictionary, string key, string? value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        if (!String.IsNullOrEmpty(value))
            dictionary[key] = value;
    }

    /// <summary>
    /// Adds or overwrites the existing value.
    /// </summary>
    public static void AddOrUpdate<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
    {
        dictionary.AddOrUpdate(key, value, (oldkey, oldvalue) => value);
    }

    public static bool ContainsKeyWithValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, params TValue[] values)
    {
        if (values.Length == 0)
            return false;

        TValue? temp;
        try
        {
            if (!dictionary.TryGetValue(key, out temp) || temp is null)
                return false;
        }
        catch (ArgumentNullException)
        {
            return false;
        }

        return values.Any(v => temp.Equals(v));
    }

    public static TValue? TryGetAndReturn<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        if (!dictionary.TryGetValue(key, out var value))
            value = default;

        return value;
    }

    public static TValue? GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        dictionary.TryGetValue(key, out var obj);
        return obj;
    }

    public static bool CollectionEquals<TValue>(this IDictionary<string, TValue>? source, IDictionary<string, TValue>? other)
    {
        if (source is null && other is null)
            return true;

        if (source is null || other is null)
            return false;

        if (source.Count != other.Count)
            return false;

        foreach (string key in source.Keys)
        {
            var sourceValue = source[key];
            if (!other.TryGetValue(key, out var otherValue))
                return false;

            if (sourceValue is null && otherValue is null)
                return true;

            if (sourceValue != null && sourceValue.Equals(otherValue))
                return false;
        }

        return true;
    }


    public static int GetCollectionHashCode<TValue>(this IDictionary<string, TValue> source, IList<string>? exclusions = null)
    {
        string? assemblyQualifiedName = typeof(TValue).AssemblyQualifiedName;
        int hashCode = assemblyQualifiedName?.GetHashCode() ?? 0;

        var keyValuePairHashes = new List<int>(source.Keys.Count);

        foreach (string key in source.Keys.OrderBy(x => x))
        {
            if (exclusions is not null && exclusions.Contains(key))
                continue;

            var item = source[key];
            unchecked
            {
                int kvpHash = key.GetHashCode();
                kvpHash = (kvpHash * 397) ^ (item?.GetHashCode() ?? 0);
                keyValuePairHashes.Add(kvpHash);
            }
        }

        keyValuePairHashes.Sort();
        foreach (int kvpHash in keyValuePairHashes)
        {
            unchecked
            {
                hashCode = (hashCode * 397) ^ kvpHash;
            }
        }

        return hashCode;
    }

    public static T? GetValueOrDefault<T>(this IDictionary<string, string> source, string key, T? defaultValue = default)
    {
        if (!source.ContainsKey(key))
            return defaultValue;

        object data = source[key];
        if (data is T variable)
            return variable;

        if (data is null)
            return defaultValue;

        try
        {
            return data.ToType<T>();
        }
        catch { }

        return defaultValue;
    }

    public static string GetString(this IDictionary<string, string> source, string name)
    {
        return source.GetString(name, String.Empty);
    }

    public static string GetString(this IDictionary<string, string> source, string name, string @default)
    {
        if (!source.TryGetValue(name, out string? value) || value is null)
            return @default;

        return value;
    }
}
