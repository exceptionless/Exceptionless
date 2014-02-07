using System;
using System.Collections;
using System.Collections.Generic;

namespace CodeSmith.Core.Collections
{
#if PFX_LEGACY_3_5
    public interface IReadOnlyCollection<T> : IEnumerable<T>
#else
    public interface IReadOnlyCollection<out T> : IEnumerable<T>
#endif
    {
        /// <summary>
        /// Gets the item with the specified index.
        /// </summary>
        /// <returns>
        /// The item with the specified index.
        /// </returns>
        T this[int index] { get; }

        /// <summary>
        /// How many items are in the collection.
        /// </summary>
        int Count { get; }
    }

    public class ReadOnlyCollection<T> : IReadOnlyCollection<T>
    {
        protected readonly IList<T> _items;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedObjectCollection{T}"/> class.
        /// </summary>
        /// <param name="items">The items from which the elements are copied.</param>
        public ReadOnlyCollection(IEnumerable<T> items)
        {
            _items = new List<T>(items);
        }

        public T this[int index]
        {
            get { return _items[index]; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }
}
