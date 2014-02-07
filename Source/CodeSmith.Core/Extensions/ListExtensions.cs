using System;
using System.Collections.Generic;

namespace CodeSmith.Core.Extensions {
    public static class ListExtensions {
        /// <summary>
        /// Adds the elements of the specified collection to the end of the list.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to which the items should be added.</param>
        /// <param name="collection">The collection whose elements should be added to the end of the list. The collection itself cannot be null, but it can contain elements that are null, if type <typeparamref name="T"/> is a reference type.</param>
        public static void AddRange<T>(this IList<T> list, IEnumerable<T> collection) {
            if (list == null)
                throw new ArgumentNullException("list");
            if (collection == null)
                throw new ArgumentNullException("collection");

            // Try to reuse AddRange from List<T>
            var concreteList = list as List<T>;
            if (concreteList != null) {
                concreteList.AddRange(collection);
            } else {
                // Perform add range manually
                foreach (var item in collection)
                    list.Add(item);
            }
        }

        /// <summary>
        /// Inserts the elements of a collection into the list at the specified index.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to which the items should be inserted.</param>
        /// <param name="index">The zero-based index at which the new elements should be inserted..</param>
        /// <param name="collection">The collection whose elements should be inserted into the list. The collection itself cannot be null, but it can contain elements that are null, if type <typeparamref name="T"/>  is a reference type.</param>
        public static void InsertRange<T>(this IList<T> list, int index, IEnumerable<T> collection) {
            if (list == null)
                throw new ArgumentNullException("list");
            if (collection == null)
                throw new ArgumentNullException("collection");

            // Try to reuse InsertRange from List<T>
            var concreteList = list as List<T>;
            if (concreteList != null) {
                concreteList.InsertRange(index, collection);
            } else {
                // Perform insert range manually
                int offset = 0;
                foreach (var item in collection)
                    list.Insert(index + offset++, item);
            }
        }

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list from with the items should be removed.</param>
        /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the elements to remove.</param>
        /// <returns>The number of elements removed from the <see cref="IList{T}"/>.</returns>
        public static int RemoveAll<T>(this IList<T> list, Predicate<T> match) {
            if (list == null)
                throw new ArgumentNullException("list");
            if (match == null)
                throw new ArgumentNullException("match");

            // Try to reuse RemoveAll from List<T>
            var concreteList = list as List<T>;
            if (concreteList != null) {
                return concreteList.RemoveAll(match);
            } else {
                // Perform remove all manually
                int removed = 0;
                for (int i = list.Count - 1; i >= 0; i--) {
                    if (match(list[i])) {
                        list.RemoveAt(i);
                        removed++;
                    }
                }
                return removed;
            }
        }
    }
}