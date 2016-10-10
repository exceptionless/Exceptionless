using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Extensions {
    public static class EnumerableExtensions {
        public static IReadOnlyCollection<T> UnionOriginalAndModified<T>(this IReadOnlyCollection<ModifiedDocument<T>> documents) where T : class, new() {
            return documents.Select(d => d.Value).Union(documents.Select(d => d.Original).Where(d => d != null)).ToList();
        }

        public static bool Contains<T>(this IEnumerable<T> enumerable, Func<T, bool> function) {
            var a = enumerable.FirstOrDefault(function);
            var b = default(T);
            return !Equals(a, b);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) {
            return source.DistinctBy(keySelector, EqualityComparer<TKey>.Default);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            return DistinctByImpl(source, keySelector, comparer);
        }

        private static IEnumerable<TSource> DistinctByImpl<TSource, TKey>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) {
            var knownKeys = new HashSet<TKey>(comparer);
            foreach (var element in source)
                if (knownKeys.Add(keySelector(element)))
                    yield return element;
        }

        public static void AddRange<T>(this ICollection<T> list, IEnumerable<T> range) {
            foreach (var r in range)
                list.Add(r);
        }

        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) {
            foreach (var item in collection ?? new List<T>())
                action(item);
        }

        public static bool CollectionEquals<T>(this IEnumerable<T> source, IEnumerable<T> other) {
            var sourceEnumerator = source.GetEnumerator();
            var otherEnumerator = other.GetEnumerator();

            while (sourceEnumerator.MoveNext()) {
                if (!otherEnumerator.MoveNext()) {
                    // counts differ
                    return false;
                }

                if (sourceEnumerator.Current.Equals(otherEnumerator.Current)) {
                    // values aren't equal
                    return false;
                }
            }

            if (otherEnumerator.MoveNext()) {
                // counts differ
                return false;
            }

            return true;
        }

        public static int GetCollectionHashCode<T>(this IEnumerable<T> source) {
            var assemblyQualifiedName = typeof(T).AssemblyQualifiedName;
            int hashCode = assemblyQualifiedName?.GetHashCode() ?? 0;

            foreach (var item in source) {
                if (item == null)
                    continue;

                unchecked {
                    hashCode = (hashCode * 397) ^ item.GetHashCode();
                }
            }

            return hashCode;
        }
    }
}
