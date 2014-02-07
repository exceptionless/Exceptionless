using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace CodeSmith.Core.Collections
{
#if !PFX_LEGACY_3_5
    public class WrappedObservableList<TBase, TActual> : IObservableList<TBase>, IList
        where TBase : class
        where TActual : class, TBase
    {
        protected readonly ObservableList<TActual> _innerList;

        public WrappedObservableList()
            : this(null)
        { }

        public WrappedObservableList(IEnumerable<TBase> data)
            : this(data == null ? null : data.Cast<TActual>())
        { }

        public WrappedObservableList(IEnumerable<TActual> data)
        {
            _innerList = data == null ?
                new ObservableList<TActual>()
                : new ObservableList<TActual>(data);
            _innerList.CollectionChanged += InnerListChanged;
            _innerList.PropertyChanged += InnerListPropertyChanged;
        }

        #region IList<T>

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">
        /// The zero-based index of the element to get or set.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="index" /> is equal to or greater than <see cref="P:System.Collections.Generic.List`1.Count" />. 
        /// </exception>
        public TBase this[int index]
        {
            get { return _innerList[index]; }
            set { _innerList[index] = (TActual)value; }
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The zero-based index of the first occurrence of <paramref name="item" /> within the entire <see cref="T:System.Collections.Generic.List`1" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public int IndexOf(TBase item)
        {
            return _innerList.IndexOf((TActual)item);
        }

        /// <summary>
        /// Inserts an element into the <see cref="T:System.Collections.Generic.List`1" /> at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index at which <paramref name="item" /> should be inserted.
        /// </param>
        /// <param name="item">
        /// The object to insert. The value can be <c>null</c> for reference types.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="index" /> is greater than <see cref="P:System.Collections.Generic.List`1.Count" />.
        /// </exception>
        public void Insert(int index, TBase item)
        {
            _innerList.Insert(index, (TActual)item);
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="index">
        /// The zero-based index of the element to remove.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="index" /> is equal to or greater than <see cref="P:System.Collections.Generic.List`1.Count" />.
        /// </exception>
        public void RemoveAt(int index)
        {
            _innerList.RemoveAt(index);
        }

        #endregion IList<T>

        #region ICollection<T>

        /// <summary>
        /// Gets the number of elements actually contained in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The number of elements actually contained in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public int Count
        {
            get { return _innerList.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns><c>true</c> if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, <c>false</c>.
        ///   </returns>
        bool ICollection<TBase>.IsReadOnly
        {
            get { return ((ICollection<TBase>)_innerList).IsReadOnly; }
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="item">
        /// The object to be added to the end of the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public void Add(TBase item)
        {
            _innerList.Add((TActual)item);
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        public void Clear()
        {
            _innerList.Clear();
        }

        /// <summary>
        /// Determines whether an element is in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.List`1" />; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public bool Contains(TBase item)
        {
            return _innerList.Contains(item);
        }

        /// <summary>
        /// Copies the entire <see cref="T:System.Collections.Generic.List`1" /> to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.List`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">
        /// The zero-based index in <paramref name="array" /> at which copying begins.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex" /> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="arrayIndex" /> is equal to or greater than the length of <paramref name="array" />.
        /// 
        /// -or-
        /// 
        /// The number of elements in the source <see cref="T:System.Collections.Generic.List`1" /> is greater than the available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.
        /// </exception>
        public void CopyTo(TBase[] array, int arrayIndex)
        {
            _innerList.CopyTo((TActual[])array, arrayIndex);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="item" /> is successfully removed; otherwise, <c>false</c>.  This method also returns <c>false</c> if <paramref name="item" /> was not found in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        /// <param name="item">
        /// The object to remove from the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public bool Remove(TBase item)
        {
            return _innerList.Remove((TActual)item);
        }

        #endregion ICollection<T>

        #region IEnumerable<T>

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.List`1.Enumerator" /> for the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public IEnumerator<TBase> GetEnumerator()
        {
            return ((IEnumerable<TBase>)_innerList).GetEnumerator();
        }

        #endregion IEnumerable<T>

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_innerList).GetEnumerator();
        }

        #endregion IEnumerable

        #region IList

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The property is set and the <see cref="T:System.Collections.Generic.IList`1"/> is read-only.
        /// </exception>
        object IList.this[int index]
        {
            get { return ((IList)_innerList)[index]; }
            set { ((IList)_innerList)[index] = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, <c>false</c>.
        /// </returns>
        bool IList.IsReadOnly
        {
            get { return ((IList)_innerList).IsReadOnly; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList"/> has a fixed size.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="T:System.Collections.IList"/> has a fixed size; otherwise, <c>false</c>.
        /// </returns>
        bool IList.IsFixedSize
        {
            get { return ((IList)_innerList).IsFixedSize; }
        }

        /// <summary>
        /// Adds an item to the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <param name="value">The object to add to the <see cref="T:System.Collections.IList"/>.</param>
        /// <returns>
        /// The position into which the new element was inserted, or -1 to indicate that the item was not inserted into the collection,
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. 
        /// </exception>
        int IList.Add(object value)
        {
            return ((IList)_innerList).Add(value);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.IList"/> contains a specific value.
        /// </summary>
        /// <param name="value">The object to locate in the <see cref="T:System.Collections.IList"/>.</param>
        /// <returns>
        /// <c>true</c> if the <see cref="T:System.Object"/> is found in the <see cref="T:System.Collections.IList"/>; otherwise, <c>false</c>.
        /// </returns>
        bool IList.Contains(object value)
        {
            return ((IList)_innerList).Contains(value);
        }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <param name="value">The object to locate in the <see cref="T:System.Collections.IList"/>.</param>
        /// <returns>
        /// The index of <paramref name="value"/> if found in the list; otherwise, -1.
        /// </returns>
        int IList.IndexOf(object value)
        {
            return ((IList)_innerList).IndexOf(value);
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.IList"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="value"/> should be inserted.</param>
        /// <param name="value">The object to insert into the <see cref="T:System.Collections.IList"/>.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.IList"/>. 
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. 
        /// </exception>
        /// <exception cref="T:System.NullReferenceException">
        /// <paramref name="value"/> is <c>null</c> reference in the <see cref="T:System.Collections.IList"/>.
        /// </exception>
        void IList.Insert(int index, object value)
        {
            ((IList)_innerList).Insert(index, value);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.IList"/>.
        /// </summary>
        /// <param name="value">The object to remove from the <see cref="T:System.Collections.IList"/>.</param>
        /// <exception cref="T:System.NotSupportedException">
        /// The <see cref="T:System.Collections.IList"/> is read-only.-or- The <see cref="T:System.Collections.IList"/> has a fixed size. 
        /// </exception>
        void IList.Remove(object value)
        {
            ((IList)_innerList).Remove(value);
        }

        #endregion IList

        #region ICollection

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </summary>
        /// <returns>
        /// An object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection"/>.
        /// </returns>
        object ICollection.SyncRoot
        {
            get { return ((ICollection)_innerList).SyncRoot; }
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// <c>true</c> if access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe); otherwise, <c>false</c>.
        /// </returns>
        bool ICollection.IsSynchronized
        {
            get { return ((ICollection)_innerList).IsSynchronized; }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array"/> is <c>null</c>. 
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than zero. 
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="array"/> is multidimensional.-or- The number of elements in the source <see cref="T:System.Collections.ICollection"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>. 
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// The type of the source <see cref="T:System.Collections.ICollection"/> cannot be cast automatically to the type of the destination <paramref name="array"/>. 
        /// </exception>
        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)_innerList).CopyTo(array, index);
        }

        #endregion ICollection

        #region List
        /// <summary>
        /// Adds the elements of the specified collection to the end of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements should be added to the end of the <see cref="T:System.Collections.Generic.List`1" />. The collection itself cannot be <c>null</c>, but it can contain elements that are <c>null</c>, if type T is a reference type.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="collection" /> is <c>null</c>.
        /// </exception>
        public void AddRange(IEnumerable<TBase> collection)
        {
            _innerList.AddRange((IEnumerable<TActual>)collection);
        }

        /// <summary>
        /// Returns a read-only <see cref="T:System.Collections.Generic.IList`1" /> wrapper for the current collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.ObjectModel.ReadOnlyCollection`1" /> that acts as a read-only wrapper around the current <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public ReadOnlyCollection<TBase> AsReadOnly()
        {
            return new ReadOnlyCollection<TBase>(_innerList.Cast<TBase>().ToList());
        }

        /// <summary>
        /// Searches a range of elements in the sorted <see cref="T:System.Collections.Generic.List`1" /> for an element using the specified comparer and returns the zero-based index of the element.
        /// </summary>
        /// <returns>
        /// The zero-based index of <paramref name="item" /> in the sorted <see cref="T:System.Collections.Generic.List`1" />, if <paramref name="item" /> is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="item" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.List`1.Count" />.
        /// </returns>
        /// <param name="index">
        /// The zero-based starting index of the range to search.
        /// </param>
        /// <param name="count">
        /// The length of the range to search.
        /// </param>
        /// <param name="item">
        /// The object to locate. The value can be <c>null</c> for reference types.
        /// </param>
        /// <param name="comparer">
        /// The <see cref="T:System.Collections.Generic.IComparer`1" /> implementation to use when comparing elements, or <c>null</c> to use the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" />.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0. 
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="index" /> and <paramref name="count" /> do not denote a valid range in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        /// <exception cref="T:System.InvalidOperationException">
        /// <paramref name="comparer" /> is <c>null</c>, and the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        public int BinarySearch(int index, int count, TBase item, IComparer<TBase> comparer)
        {
            return _innerList.BinarySearch(index, count, (TActual)item, comparer);
        }

        /// <summary>
        /// Searches the entire sorted <see cref="T:System.Collections.Generic.List`1" /> for an element using the default comparer and returns the zero-based index of the element.
        /// </summary>
        /// <returns>
        /// The zero-based index of <paramref name="item" /> in the sorted <see cref="T:System.Collections.Generic.List`1" />, if <paramref name="item" /> is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="item" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.List`1.Count" />.
        /// </returns>
        /// <param name="item">
        /// The object to locate. The value can be <c>null</c> for reference types.
        /// </param>
        /// <exception cref="T:System.InvalidOperationException">
        /// The default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        public int BinarySearch(TBase item)
        {
            return _innerList.BinarySearch((TActual)item);
        }

        /// <summary>
        /// Searches the entire sorted <see cref="T:System.Collections.Generic.List`1" /> for an element using the specified comparer and returns the zero-based index of the element.
        /// </summary>
        /// <returns>
        /// The zero-based index of <paramref name="item" /> in the sorted <see cref="T:System.Collections.Generic.List`1" />, if <paramref name="item" /> is found; otherwise, a negative number that is the bitwise complement of the index of the next element that is larger than <paramref name="item" /> or, if there is no larger element, the bitwise complement of <see cref="P:System.Collections.Generic.List`1.Count" />.
        /// </returns>
        /// <param name="item">
        /// The object to locate. The value can be <c>null</c> for reference types.
        /// </param>
        /// <param name="comparer">
        /// The <see cref="T:System.Collections.Generic.IComparer`1" /> implementation to use when comparing elements.
        /// 
        /// -or-
        /// <c>null</c> to use the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" />.
        /// </param>
        /// <exception cref="T:System.InvalidOperationException">
        /// <paramref name="comparer" /> is <c>null</c>, and the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        public int BinarySearch(TBase item, IComparer<TBase> comparer)
        {
            return _innerList.BinarySearch((TActual)item, comparer);
        }

        /// <summary>
        /// Converts the elements in the current <see cref="T:System.Collections.Generic.List`1" /> to another type, and returns a list containing the converted elements.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.List`1" /> of the target type containing the converted elements from the current <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        /// <param name="converter">
        /// A <see cref="T:System.Converter`2" /> delegate that converts each element from one type to another type.
        /// </param>
        /// <typeparam name="TOutput">
        /// The type of the elements of the target array.
        /// </typeparam>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="converter" /> is <c>null</c>.
        /// </exception>
        public List<TOutput> ConvertAll<TOutput>(Converter<TBase, TOutput> converter)
        {
            return _innerList.ConvertAll(converter);
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.Generic.List`1" /> contains elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <returns><c>true</c> if the <see cref="T:System.Collections.Generic.List`1" /> contains one or more elements that match the conditions defined by the specified predicate; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the elements to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public bool Exists(Predicate<TBase> match)
        {
            return _innerList.Exists(match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type T.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public TBase Find(Predicate<TBase> match)
        {
            return _innerList.Find(match);
        }

        /// <summary>
        /// Retrieves all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.List`1" /> containing all the elements that match the conditions defined by the specified predicate, if found; otherwise, an empty <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the elements to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public List<TBase> FindAll(Predicate<TBase> match)
        {
            return _innerList.FindAll(match).Cast<TBase>().ToList();
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public int FindIndex(Predicate<TBase> match)
        {
            return _innerList.FindIndex(match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that extends from the specified index to the last element.
        /// </summary>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="startIndex">
        /// The zero-based starting index of the search.
        /// </param>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="startIndex" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public int FindIndex(int startIndex, Predicate<TBase> match)
        {
            return _innerList.FindIndex(startIndex, match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that starts at the specified index and contains the specified number of elements.
        /// </summary>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="startIndex">
        /// The zero-based starting index of the search.
        /// </param>
        /// <param name="count">
        /// The number of elements in the section to search.
        /// </param>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="startIndex" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="startIndex" /> and <paramref name="count" /> do not specify a valid section in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public int FindIndex(int startIndex, int count, Predicate<TBase> match)
        {
            return _innerList.FindIndex(startIndex, count, match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the last occurrence within the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The last element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type T.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public TBase FindLast(Predicate<TBase> match)
        {
            return _innerList.FindLast(match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public int FindLastIndex(Predicate<TBase> match)
        {
            return _innerList.FindLastIndex(match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that extends from the first element to the specified index.
        /// </summary>
        /// <returns>
        /// The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="startIndex">
        /// The zero-based starting index of the backward search.
        /// </param>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="startIndex" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public int FindLastIndex(int startIndex, Predicate<TBase> match)
        {
            return _innerList.FindLastIndex(startIndex, match);
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that contains the specified number of elements and ends at the specified index.
        /// </summary>
        /// <returns>
        /// The zero-based index of the last occurrence of an element that matches the conditions defined by <paramref name="match" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="startIndex">
        /// The zero-based starting index of the backward search.
        /// </param>
        /// <param name="count">
        /// The number of elements in the section to search.
        /// </param>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="startIndex" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="startIndex" /> and <paramref name="count" /> do not specify a valid section in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public int FindLastIndex(int startIndex, int count, Predicate<TBase> match)
        {
            return _innerList.FindLastIndex(startIndex, count, match);
        }

        /// <summary>
        /// Performs the specified action on each element of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="action">
        /// The <see cref="T:System.Action`1" /> delegate to perform on each element of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="action" /> is <c>null</c>.
        /// </exception>
        public void ForEach(Action<TBase> action)
        {
            _innerList.ForEach(action);
        }

        /// <summary>
        /// Creates a shallow copy of a range of elements in the source <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// A shallow copy of a range of elements in the source <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        /// <param name="index">
        /// The zero-based <see cref="T:System.Collections.Generic.List`1" /> index at which the range starts.
        /// </param>
        /// <param name="count">
        /// The number of elements in the range.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="index" /> and <paramref name="count" /> do not denote a valid range of elements in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public List<TBase> GetRange(int index, int count)
        {
            return _innerList.GetRange(index, count).Cast<TBase>().ToList();
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that extends from the specified index to the last element.
        /// </summary>
        /// <returns>
        /// The zero-based index of the first occurrence of <paramref name="item" /> within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that extends from <paramref name="index" /> to the last element, if found; otherwise, –1.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        /// <param name="index">
        /// The zero-based starting index of the search.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public int IndexOf(TBase item, int index)
        {
            return _innerList.IndexOf((TActual)item, index);
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that starts at the specified index and contains the specified number of elements.
        /// </summary>
        /// <returns>
        /// The zero-based index of the first occurrence of <paramref name="item" /> within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that starts at <paramref name="index" /> and contains <paramref name="count" /> number of elements, if found; otherwise, –1.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        /// <param name="index">
        /// The zero-based starting index of the search.
        /// </param>
        /// <param name="count">
        /// The number of elements in the section to search.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="index" /> and <paramref name="count" /> do not specify a valid section in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public int IndexOf(TBase item, int index, int count)
        {
            return _innerList.IndexOf((TActual)item, index, count);
        }

        /// <summary>
        /// Inserts the elements of a collection into the <see cref="T:System.Collections.Generic.List`1" /> at the specified index.
        /// </summary>
        /// <param name="index">
        /// The zero-based index at which the new elements should be inserted.
        /// </param>
        /// <param name="collection">
        /// The collection whose elements should be inserted into the <see cref="T:System.Collections.Generic.List`1" />. The collection itself cannot be <c>null</c>, but it can contain elements that are <c>null</c>, if type T is a reference type.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="collection" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="index" /> is greater than <see cref="P:System.Collections.Generic.List`1.Count" />.
        /// </exception>
        public void InsertRange(int index, IEnumerable<TBase> collection)
        {
            _innerList.InsertRange(index, collection.Cast<TActual>());
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last occurrence within the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The zero-based index of the last occurrence of <paramref name="item" /> within the entire the <see cref="T:System.Collections.Generic.List`1" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public int LastIndexOf(TBase item)
        {
            return _innerList.LastIndexOf((TActual)item);
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that extends from the first element to the specified index.
        /// </summary>
        /// <returns>
        /// The zero-based index of the last occurrence of <paramref name="item" /> within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that extends from the first element to <paramref name="index" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        /// <param name="index">
        /// The zero-based starting index of the backward search.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />. 
        /// </exception>
        public int LastIndexOf(TBase item, int index)
        {
            return _innerList.LastIndexOf((TActual)item, index);
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that contains the specified number of elements and ends at the specified index.
        /// </summary>
        /// <returns>
        /// The zero-based index of the last occurrence of <paramref name="item" /> within the range of elements in the <see cref="T:System.Collections.Generic.List`1" /> that contains <paramref name="count" /> number of elements and ends at <paramref name="index" />, if found; otherwise, –1.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        /// <param name="index">
        /// The zero-based starting index of the backward search.
        /// </param>
        /// <param name="count">
        /// The number of elements in the section to search.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is outside the range of valid indexes for the <see cref="T:System.Collections.Generic.List`1" />.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="index" /> and <paramref name="count" /> do not specify a valid section in the <see cref="T:System.Collections.Generic.List`1" />. 
        /// </exception>
        public int LastIndexOf(TBase item, int index, int count)
        {
            return _innerList.LastIndexOf((TActual)item, index, count);
        }

        /// <summary>
        /// Removes the all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <returns>
        /// The number of elements removed from the <see cref="T:System.Collections.Generic.List`1" /> .
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the elements to remove.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public int RemoveAll(Predicate<TBase> match)
        {
            return _innerList.RemoveAll(match);
        }

        /// <summary>
        /// Removes a range of elements from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="index">
        /// The zero-based starting index of the range of elements to remove.
        /// </param>
        /// <param name="count">
        /// The number of elements to remove.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="index" /> and <paramref name="count" /> do not denote a valid range of elements in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </exception>
        public void RemoveRange(int index, int count)
        {
            _innerList.RemoveRange(index, count);
        }

        /// <summary>
        /// Determines whether every element in the <see cref="T:System.Collections.Generic.List`1" /> matches the conditions defined by the specified predicate.
        /// </summary>
        /// <returns><c>true</c> if every element in the <see cref="T:System.Collections.Generic.List`1" /> matches the conditions defined by the specified predicate; otherwise, <c>false</c>. If the list has no elements, the return value is <c>true</c>.
        /// </returns>
        /// <param name="match">
        /// The <see cref="T:System.Predicate`1" /> delegate that defines the conditions to check against the elements.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="match" /> is <c>null</c>.
        /// </exception>
        public bool TrueForAll(Predicate<TBase> match)
        {
            return _innerList.TrueForAll(match);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.List`1" /> to a new array.
        /// </summary>
        /// <returns>
        /// An array containing copies of the elements of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public TBase[] ToArray()
        {
            return _innerList.ToArray();
        }

        /// <summary>
        /// Sets the capacity to the actual number of elements in the <see cref="T:System.Collections.Generic.List`1" />, if that number is less than a threshold value.
        /// </summary>
        public void TrimExcess()
        {
            _innerList.TrimExcess();
        }

        /// <summary>
        /// Reverses the order of the elements in the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        public void Reverse()
        {
            _innerList.Reverse();
        }

        /// <summary>
        /// Reverses the order of the elements in the specified range.
        /// </summary>
        /// <param name="index">
        /// The zero-based starting index of the range to reverse.
        /// </param>
        /// <param name="count">
        /// The number of elements in the range to reverse.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0. 
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="index" /> and <paramref name="count" /> do not denote a valid range of elements in the <see cref="T:System.Collections.Generic.List`1" />. 
        /// </exception>
        public void Reverse(int index, int count)
        {
            _innerList.Reverse(index, count);
        }

        /// <summary>
        /// Sorts the elements in the entire <see cref="T:System.Collections.Generic.List`1" /> using the default comparer.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">
        /// The default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        public void Sort()
        {
            _innerList.Sort();
        }

        /// <summary>
        /// Sorts the elements in the entire <see cref="T:System.Collections.Generic.List`1" /> using the specified comparer.
        /// </summary>
        /// <param name="comparer">
        /// The <see cref="T:System.Collections.IComparer" /> implementation to use when comparing elements.
        /// </param>
        /// <exception cref="T:System.InvalidOperationException">
        /// <paramref name="comparer" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// The implementation of <paramref name="comparer" /> caused an error during the sort. For example, <paramref name="comparer" /> might not return 0 when comparing an item with itself.
        /// </exception>
        public void Sort(IComparer comparer)
        {
            _innerList.Sort(comparer);
        }


        /// <summary>
        /// Sorts the elements in the entire <see cref="T:System.Collections.Generic.List`1" /> using the specified comparer.
        /// </summary>
        /// <param name="comparer">
        /// The <see cref="T:System.Collections.Generic.IComparer`1" /> implementation to use when comparing elements, or <c>null</c> to use the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" />.
        /// </param>
        /// <exception cref="T:System.InvalidOperationException">
        /// <paramref name="comparer" /> is <c>null</c>, and the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// The implementation of <paramref name="comparer" /> caused an error during the sort. For example, <paramref name="comparer" /> might not return 0 when comparing an item with itself.
        /// </exception>
        public void Sort(IComparer<TBase> comparer)
        {
            _innerList.Sort(comparer);
        }

        /// <summary>
        /// Sorts the elements in a range of elements in <see cref="T:System.Collections.Generic.List`1" /> using the specified comparer.
        /// </summary>
        /// <param name="index">
        /// The zero-based starting index of the range to sort.
        /// </param>
        /// <param name="count">
        /// The length of the range to sort.
        /// </param>
        /// <param name="comparer">
        /// The <see cref="T:System.Collections.Generic.IComparer`1" /> implementation to use when comparing elements, or <c>null</c> to use the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" />.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="index" /> is less than 0.
        /// 
        /// -or-
        /// <paramref name="count" /> is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="index" /> and <paramref name="count" /> do not specify a valid range in the <see cref="T:System.Collections.Generic.List`1" />.
        /// 
        /// -or-
        /// 
        /// The implementation of <paramref name="comparer" /> caused an error during the sort. For example, <paramref name="comparer" /> might not return 0 when comparing an item with itself.
        /// </exception>
        /// <exception cref="T:System.InvalidOperationException">
        /// <paramref name="comparer" /> is <c>null</c>, and the default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        public void Sort(int index, int count, IComparer<TBase> comparer)
        {
            _innerList.Sort(index, count, comparer);
        }

        /// <summary>
        /// Sorts the elements in the entire <see cref="T:System.Collections.Generic.List`1" /> using the specified <see cref="T:System.Comparison`1" />.
        /// </summary>
        /// <param name="comparison">
        /// The <see cref="T:System.Comparison`1" /> to use when comparing elements.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="comparison" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// The implementation of <paramref name="comparison" /> caused an error during the sort. For example, <paramref name="comparison" /> might not return 0 when comparing an item with itself.
        /// </exception>
        public void Sort(Comparison<TBase> comparison)
        {
            _innerList.Sort(comparison);
        }

        #endregion List

        #region INotifyCollectionChanged

        /// <summary>
        /// Occurs when an item is added, removed, changed, moved, or the entire list is refreshed.
        /// </summary>
        [field: NonSerializedAttribute]
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private void InnerListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null)
                CollectionChanged(sender, e);
        }

        #endregion INotifyCollectionChanged

        #region INotifyPropertyChanged

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        [field: NonSerializedAttribute]
        public event PropertyChangedEventHandler PropertyChanged;

        private void InnerListPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(sender, e);
        }

        #endregion INotifyPropertyChanged
    }
#endif
}