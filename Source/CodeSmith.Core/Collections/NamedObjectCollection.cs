//------------------------------------------------------------------------------
//
// Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.
// 
// The terms of use for this software are contained in the file
// named sourcelicense.txt, which can be found in the root of this distribution.
// By using this software in any fashion, you are agreeing to be bound by the
// terms of this license.
// 
// You must not remove this notice, or any other, from this software.
//
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Collections
{
    public interface INamedObjectCollection<T> : IObservableList<T> where T : INamedObject
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

    /// <summary>
    /// Implements a strongly typed collection of <see cref="INamedObject"/> elements.
    /// </summary>
    /// <remarks>
    /// <b>SchemaObjectBaseCollection</b> provides an <see cref="ObservableList{T}"/>
    /// that is strongly typed for <see cref="INamedObject"/> elements.
    /// </remarks>
    [Serializable]
    public class NamedObjectCollection<T> : ObservableList<T>, INamedObjectCollection<T>, IReadOnlyNamedObjectCollection<T> where T : INamedObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NamedObjectCollection{T}"/> class.
        /// </summary>
        public NamedObjectCollection()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedObjectCollection{T}"/> class.
        /// </summary>
        /// <param name="capacity">The number of elements that the new list can initially store.</param>
        public NamedObjectCollection(int capacity)
            : base(capacity)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedObjectCollection{T}"/> class.
        /// </summary>
        /// <param name="collection">The collection from which the elements are copied.</param>
        public NamedObjectCollection(IEnumerable<T> collection)
            : base(collection)
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
            set
            {
                int index = IndexOf(name);
                if (index >= 0)
                    this[index] = value;
                else
                    Add(value);
            }
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
