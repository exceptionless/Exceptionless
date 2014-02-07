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
using System.Collections;
using System.Collections.Generic;

namespace CodeSmith.Core.Collections
{
    /// <summary>
    /// IndexedEnumerable makes enumerating over collections much easier by implementing properties like: IsEven, IsOdd, IsLast.
    /// </summary>
    public static class IndexedEnumerable
    {
        #region Public Static Method
        /// <summary>
        /// Returns an IndexedEnumerable from any collection implementing IEnumerable&lt;T&gt;
        /// </summary>
        /// <typeparam name="T">Type of enumerable</typeparam>
        /// <param name="source">Source enumerable</param>
        /// <returns>A new IndexedEnumerable&lt;T&gt;.</returns>
        public static IndexedEnumerable<T> Create<T>(IEnumerable<T> source)
        {
            return new IndexedEnumerable<T>(source);
        }

        #endregion

        #region Extension Methods

        /// <summary>
        /// Returns an IndexedEnumerable from any collection implementing IEnumerable&lt;T&gt;
        /// </summary>
        /// <typeparam name="T">Type of enumerable</typeparam>
        /// <param name="source">Source enumerable</param>
        /// <returns>A new IndexedEnumerable&lt;T&gt;.</returns>
        public static IndexedEnumerable<T> AsIndexedEnumerable<T>(this IEnumerable<T> source)
        {
            return new IndexedEnumerable<T>(source);
        }

        #endregion
    }

    /// <summary>
    /// IndexedEnumerable makes enumerating over collections much easier by implementing properties like: IsEven, IsOdd, IsLast.
    /// </summary>
    /// <typeparam name="T">Type to iterate over</typeparam>
    public class IndexedEnumerable<T> : IEnumerable<IndexedEnumerable<T>.EntryItem>
    {
        #region Private Members

        private readonly IEnumerable<T> _enumerable;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        private IndexedEnumerable(){}

        /// <summary>
        /// Constructor that takes an IEnumerable&lt;T&gt;
        /// </summary>
        /// <param name="enumerable">The collection to enumerate.</param>
        public IndexedEnumerable(IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException("enumerable");
            }

            _enumerable = enumerable;
        }

        #endregion

        /// <summary>
        /// Returns an enumeration of Entry objects.
        /// </summary>
        public IEnumerator<EntryItem> GetEnumerator()
        {
            using (IEnumerator<T> enumerator = _enumerable.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    yield break;
                }

                int index = 0;
                bool isFirst = true;
                bool isLast = false;
                while (!isLast)
                {
                    T current = enumerator.Current;
                    isLast = !enumerator.MoveNext();
                    yield return new EntryItem(isFirst, isLast, current, index++);
                    isFirst = false;
                }
            }
        }

        /// <summary>
        /// Non-generic form of GetEnumerator.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Represents each entry returned within a collection,
        /// containing the _value and whether it is the first and/or
        /// the last entry in the collection's. enumeration
        /// </summary>
        public class EntryItem
        {
            #region Constructors

            internal EntryItem(){}

            internal EntryItem(bool isFirst, bool isLast, T value, int index)
            {
                IsFirst = isFirst;
                IsLast = isLast;
                Value = value;
                Index = index;

                IsEven = index % 2 == 0;
                IsOdd = !IsEven;
            }

            #endregion

            /// <summary>
            /// The Entry Value.
            /// </summary>
            public T Value { get; internal set; }

            /// <summary>
            /// Returns true if it is the first item in the collection.
            /// </summary>
            public bool IsFirst { get; internal set; }

            /// <summary>
            /// Returns true if it is the last item in the collection.
            /// </summary>
            public bool IsLast { get; internal set; }

            /// <summary>
            /// The index of the current item in the collection.
            /// </summary>
            public int Index { get; internal set; }

            /// <summary>
            /// Returns true if the current item has an even index
            /// </summary>
            public bool IsEven { get; internal set; }

            /// <summary>
            /// Returns true if the current item has an odd index
            /// </summary>
            public bool IsOdd { get; internal set; }

            public static implicit operator T(EntryItem item)
            {
                return item.Value;
            }
        }
    }
}