using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Collections
{
#if PFX_LEGACY_3_5
    public interface IReadOnlyNamedObjectCollection<T> : IReadOnlyCollection<T> where T : INamedObject
#else
    public interface IReadOnlyNamedObjectCollection<out T> : IReadOnlyCollection<T> where T : INamedObject
#endif
    {
        /// <summary>
        /// Gets the item with the specified name.
        /// </summary>
        /// <returns>
        /// The item with the specified name.
        /// </returns>
        T this[string name] { get; }

        /// <summary>
        /// Determines whether an element is in the collection with the specified name.
        /// </summary>
        /// <param name="name">The name of the item to locate in the collection.</param>
        /// <returns>
        ///   <c>true</c> if item is found in the collection; otherwise, <c>false</c>.
        /// </returns>
        bool Contains(string name);

        /// <summary>
        /// Determines the index of a specific item in the list.
        /// </summary>
        /// <param name="name">The name of the item to locate in the list.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        int IndexOf(string name);
    }

    public class ReadOnlyNamedObjectCollection<T> : ReadOnlyCollection<T>, IReadOnlyNamedObjectCollection<T> where T: INamedObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NamedObjectCollection{T}"/> class.
        /// </summary>
        /// <param name="items">The items from which the elements are copied.</param>
        public ReadOnlyNamedObjectCollection(IEnumerable<T> items)
            : base(items.ToList())
        {
        }

        /// <summary>
        /// Gets the item with the specified name.
        /// </summary>
        /// <returns>
        /// The item with the specified name.
        /// </returns>
        public virtual T this[string name]
        {
            get { return this.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
        }

        /// <summary>
        /// Determines whether an element is in the collection with the specified name.
        /// </summary>
        /// <param name="name">The name of the item to locate in the collection.</param>
        /// <returns>
        ///   <c>true</c> if item is found in the collection; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(string name)
        {
            return this.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines the index of a specific item in the list.
        /// </summary>
        /// <param name="name">The name of the item to locate in the list.</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        public int IndexOf(string name)
        {
            return this.IndexOf(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                sb.Append(this[i]);
                if (i < Count - 1)
                    sb.Append(", ");
            }

            return sb.ToString();
        }
    }
}
