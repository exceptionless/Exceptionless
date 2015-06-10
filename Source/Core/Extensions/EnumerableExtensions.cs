using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Extensions {
    public static class EnumerableExtensions {
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

        public static void EnsureIds<T>(this ICollection<T> values) where T : class, IIdentity {
            if (values == null)
                return;

            foreach (var value in values.Where(value => value.Id == null))
                value.Id = ObjectId.GenerateNewId().ToString();
        }

        public static void SetDates<T>(this IEnumerable<T> values) where T : class, IHaveDates {
            if (values == null)
                return;

            foreach (var value in values) {
                if (value.CreatedUtc == DateTime.MinValue)
                    value.CreatedUtc = DateTime.UtcNow;
                value.ModifiedUtc = DateTime.UtcNow;
            }
        }

        public static void SetCreatedDates<T>(this IEnumerable<T> values) where T : class, IHaveCreatedDate {
            if (values == null)
                return;

            foreach (var value in values) {
                if (value.CreatedUtc == DateTime.MinValue)
                    value.CreatedUtc = DateTime.UtcNow;
            }
        }
    }
}