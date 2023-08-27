using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Extensions;

public static class EnumerableExtensions
{
    public static IReadOnlyCollection<T> UnionOriginalAndModified<T>(this IReadOnlyCollection<ModifiedDocument<T>> documents) where T : class, new()
    {
        return documents.Select(d => d.Value).Union(documents.Select(d => d.Original).Where(d => d is not null)).ToList();
    }

    public static bool Contains<T>(this IEnumerable<T> enumerable, Func<T, bool> function)
    {
        var a = enumerable.FirstOrDefault(function);
        var b = default(T);
        return !Equals(a, b);
    }

    public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
    {
        foreach (var item in collection ?? new List<T>())
            action(item);
    }

    public static bool CollectionEquals<T>(this IEnumerable<T>? source, IEnumerable<T>? other)
    {
        if (source is null && other is null)
            return true;

        if (source is null || other is null)
            return false;

        using var sourceEnumerator = source.GetEnumerator();
        using var otherEnumerator = other.GetEnumerator();

        while (sourceEnumerator.MoveNext())
        {
            if (!otherEnumerator.MoveNext())
            {
                // counts differ
                return false;
            }

            if (sourceEnumerator.Current is not null && sourceEnumerator.Current.Equals(otherEnumerator.Current))
            {
                // values aren't equal
                return false;
            }
        }

        return !otherEnumerator.MoveNext();
    }

    public static int GetCollectionHashCode<T>(this IEnumerable<T> source)
    {
        string? assemblyQualifiedName = typeof(T).AssemblyQualifiedName;
        int hashCode = assemblyQualifiedName?.GetHashCode() ?? 0;

        foreach (var item in source)
        {
            if (item is null)
                continue;

            unchecked
            {
                hashCode = (hashCode * 397) ^ item.GetHashCode();
            }
        }

        return hashCode;
    }

    /// <summary>
    /// Helper method for paging objects in a given source
    /// </summary>
    /// <typeparam name="T">type of object in source collection</typeparam>
    /// <param name="source">source collection to be paged</param>
    /// <param name="pageSize">page size</param>
    /// <returns>a collection of sub-collections by page size</returns>
    public static IEnumerable<ReadOnlyCollection<T>> Page<T>(this IEnumerable<T> source, int pageSize)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        using var enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var currentPage = new List<T>(pageSize) { enumerator.Current };

            while (currentPage.Count < pageSize && enumerator.MoveNext())
            {
                currentPage.Add(enumerator.Current);
            }

            yield return new ReadOnlyCollection<T>(currentPage);
        }
    }
}
