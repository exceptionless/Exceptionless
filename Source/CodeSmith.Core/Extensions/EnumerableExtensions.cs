using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if !EMBEDDED
using CodeSmith.Core.Helpers;

namespace CodeSmith.Core.Extensions {
    public
#else

namespace Exceptionless.Extensions {
    internal
#endif

    static class EnumerableExtensions {
        public static bool Contains<T>(this IEnumerable<T> enumerable, Func<T, bool> function) {
            var a = enumerable.FirstOrDefault(function);
            var b = default(T);
            return !Object.Equals(a, b);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) {
            return source.DistinctBy(keySelector, EqualityComparer<TKey>.Default);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) {
            if (source == null)
                throw new ArgumentNullException("source");
            if (keySelector == null)
                throw new ArgumentNullException("keySelector");
            if (comparer == null)
                throw new ArgumentNullException("comparer");

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

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> items) {
            return items == null || !items.Any();
        }

        public static IEnumerable<T> AsNullIfEmpty<T>(this IEnumerable<T> items) {
            if (items == null || !items.Any())
                return null;

            return items;
        }

#if !EMBEDDED
        public static IEnumerable<T> TakeRandom<T>(this IEnumerable<T> items, int count) {
            if (items == null)
                throw new ArgumentNullException("items");

            // Not optimal for large sets, but it works.
            return items
                .OrderBy(t => RandomHelper.Instance.Next())
                .Take(count);
        }

        public static T Random<T>(this IEnumerable<T> items, T defaultValue = default(T)) {
            if (items == null)
                return defaultValue;

            var list = items.ToList();
            int count = list.Count();
            if (count == 0)
                return defaultValue;

            return list.ElementAt(RandomHelper.Instance.Next(count));
        }

        public static IList<T> Shuffle<T>(this IList<T> list) {
            var n = list.Count;
            while (n > 1) {
                n--;
                int k = RandomHelper.Instance.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            return list;
        }
#endif

        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) {
            foreach (var item in collection)
                action(item);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> selector) {
            int index = 0;
            foreach (var item in source) {
                if (selector(item))
                    return index;

                index++;
            }

            // not found
            return -1;
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource item) {
            return IndexOf(source, item, null);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource item, IEqualityComparer<TSource> itemComparer) {
            if (source == null)
                throw new ArgumentNullException("source");

            var listOfT = source as IList<TSource>;
            if (listOfT != null)
                return listOfT.IndexOf(item);

            var list = source as IList;
            if (list != null)
                return list.IndexOf(item);

            if (itemComparer == null)
                itemComparer = EqualityComparer<TSource>.Default;

            int i = 0;
            foreach (TSource possibleItem in source) {
                if (itemComparer.Equals(item, possibleItem))
                    return i;

                i++;
            }

            return -1;
        }
    }
}