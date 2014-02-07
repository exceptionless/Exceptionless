#if !SILVERLIGHT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace CodeSmith.Core.Collections
{
    /// <summary>
    /// Represents a strongly typed list of objects that provides notifications when items get added, removed, or when the whole list is refreshed.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public class ObservableList<T> : IObservableList<T>, IList
    {
        private readonly List<T> _items;

        /// <summary>
        /// Gets a <see cref="T:System.Collections.Generic.List`1" /> wrapper around the <see cref="T:CodeSmith.Core.Collections.ObservableList`1" />.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.List`1" /> wrapper around the <see cref="T:CodeSmith.Core.Collections.ObservableList`1" />.
        /// </returns>
        protected List<T> Items
        {
            get { return _items; }
        }

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.List`1" /> class that is empty and has the default initial capacity.
        /// </summary>
        public ObservableList()
        {
            _items = new List<T>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.List`1" /> class that is empty and has the specified initial capacity.
        /// </summary>
        /// <param name="capacity">
        /// The number of elements that the new list can initially store.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="capacity" /> is less than 0. 
        /// </exception>
        public ObservableList(int capacity)
        {
            _items = new List<T>(capacity);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.List`1" /> class that contains elements copied from the specified collection and has sufficient capacity to accommodate the number of elements copied.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new list.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="collection" /> is <c>null</c>.
        /// </exception>
        public ObservableList(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentException("The collection parameter is required and cannot be null.", "collection");

            _items = new List<T>(collection);
        }

        #endregion Constructors

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
        public T this[int index]
        {
            get { return _items[index]; }
            set
            {
                T oldItem = _items[index];

                _items[index] = value;

                OnItemsChanged();
                OnCollectionReplace(index, oldItem, value);
            }
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
        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
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
        public void Insert(int index, T item)
        {
            _items.Insert(index, item);

            OnCountChanged();
            OnItemsChanged();
            OnCollectionAdd(index, item);
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
            T oldItem = _items[index];

            _items.RemoveAt(index);

            OnCountChanged();
            OnItemsChanged();
            OnCollectionRemove(index, oldItem);
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
            get { return _items.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns><c>true</c> if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, <c>false</c>.
        ///   </returns>
        bool ICollection<T>.IsReadOnly
        {
            get { return ((ICollection<T>)_items).IsReadOnly; }
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="item">
        /// The object to be added to the end of the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public void Add(T item)
        {
            _items.Add(item);

            OnCountChanged();
            OnItemsChanged();
            OnCollectionAdd(Count - 1, item);
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        public void Clear()
        {
            if (Count == 0)
                return;

            T[] oldItems = ToArray();

            _items.Clear();

            OnCountChanged();
            OnItemsChanged();
            OnCollectionRemove(0, oldItems);
        }

        /// <summary>
        /// Determines whether an element is in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.List`1" />; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="item">
        /// The object to locate in the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        /// <summary>
        /// Copies the entire <see cref="T:System.Collections.Generic.List`1" /> to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.List`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array" /> is <c>null</c>.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="array" /> index is less than 0.
        /// </exception>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="array" /> index is equal to or greater than the length of <paramref name="array" />.
        /// 
        /// -or-
        /// 
        /// The number of elements in the source <see cref="T:System.Collections.Generic.List`1" /> is greater than the available space from <paramref name="array" /> index to the end of the destination <paramref name="array" />.
        /// </exception>
        public void CopyTo(T[] array)
        {
            _items.CopyTo(array, 0);
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
        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns><c>true</c> if <paramref name="item" /> is successfully removed; otherwise, <c>false</c>.  This method also returns <c>false</c> if <paramref name="item" /> was not found in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        /// <param name="item">
        /// The object to remove from the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;

            RemoveAt(index);
            return true;
        }

        #endregion ICollection<T>

        #region IEnumerable<T>

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.List`1.Enumerator" /> for the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)_items).GetEnumerator();
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
            return ((IEnumerable)_items).GetEnumerator();
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
            get { return ((IList)_items)[index]; }
            set
            {
                T oldItem = _items[index];

                ((IList)_items)[index] = value;

                OnCountChanged();
                OnItemsChanged();
                OnCollectionReplace(index, oldItem, (T)value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, <c>false</c>.
        /// </returns>
        bool IList.IsReadOnly
        {
            get { return ((IList)_items).IsReadOnly; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IList"/> has a fixed size.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="T:System.Collections.IList"/> has a fixed size; otherwise, <c>false</c>.
        /// </returns>
        bool IList.IsFixedSize
        {
            get { return ((IList)_items).IsFixedSize; }
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
            int result = ((IList)_items).Add(value);

            OnCountChanged();
            OnItemsChanged();
            OnCollectionAdd(result, (T)value);

            return result;
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
            return ((IList)_items).Contains(value);
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
            return ((IList)_items).IndexOf(value);
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
            ((IList)_items).Insert(index, value);

            OnCountChanged();
            OnItemsChanged();
            OnCollectionAdd(index, (T)value);
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
            Remove((T)value);
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
            get { return ((ICollection)_items).SyncRoot; }
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe).
        /// </summary>
        /// <returns>
        /// <c>true</c> if access to the <see cref="T:System.Collections.ICollection"/> is synchronized (thread safe); otherwise, <c>false</c>.
        /// </returns>
        bool ICollection.IsSynchronized
        {
            get { return ((ICollection)_items).IsSynchronized; }
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
            ((ICollection)_items).CopyTo(array, index);
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
        public void AddRange(IEnumerable<T> collection)
        {
            int oldCount = Count;

            var newitems = collection.ToArray();
            _items.AddRange(newitems);

            if (oldCount == Count)
                return;

            OnCountChanged();
            OnItemsChanged();
            OnCollectionAdd(oldCount, newitems);
        }

        /// <summary>
        /// Returns a read-only <see cref="T:System.Collections.Generic.IList`1" /> wrapper for the current collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.ObjectModel.ReadOnlyCollection`1" /> that acts as a read-only wrapper around the current <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public ReadOnlyCollection<T> AsReadOnly()
        {
            return new ReadOnlyCollection<T>(this);
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
        public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
        {
            return _items.BinarySearch(index, count, item, comparer);
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
        public int BinarySearch(T item)
        {
            return _items.BinarySearch(item);
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
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            return _items.BinarySearch(item, comparer);
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
        public List<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        {
            return _items.ConvertAll(converter);
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
        public bool Exists(Predicate<T> match)
        {
            return _items.Exists(match);
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
        public T Find(Predicate<T> match)
        {
            return _items.Find(match);
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
        public List<T> FindAll(Predicate<T> match)
        {
            return _items.FindAll(match);
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
        public int FindIndex(Predicate<T> match)
        {
            return _items.FindIndex(match);
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
        public int FindIndex(int startIndex, Predicate<T> match)
        {
            return _items.FindIndex(startIndex, match);
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
        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            return _items.FindIndex(startIndex, count, match);
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
        public T FindLast(Predicate<T> match)
        {
            return _items.FindLast(match);
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
        public int FindLastIndex(Predicate<T> match)
        {
            return _items.FindLastIndex(match);
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
        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            return _items.FindLastIndex(startIndex, match);
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
        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            return _items.FindLastIndex(startIndex, count, match);
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
        public void ForEach(Action<T> action)
        {
            _items.ForEach(action);
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
        public List<T> GetRange(int index, int count)
        {
            return _items.GetRange(index, count);
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
        public int IndexOf(T item, int index)
        {
            return _items.IndexOf(item, index);
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
        public int IndexOf(T item, int index, int count)
        {
            return _items.IndexOf(item, index, count);
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
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            int oldCount = Count;

            _items.InsertRange(index, collection);

            if (oldCount == Count)
                return;

            OnCountChanged();
            OnItemsChanged();
            OnCollectionAdd(index, collection.ToArray());
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
        public int LastIndexOf(T item)
        {
            return _items.LastIndexOf(item);
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
        public int LastIndexOf(T item, int index)
        {
            return _items.LastIndexOf(item, index);
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
        public int LastIndexOf(T item, int index, int count)
        {
            return _items.LastIndexOf(item, index, count);
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
        public int RemoveAll(Predicate<T> match)
        {
            var oldItems = new Dictionary<int, T>();
            for (int i = 0; i < _items.Count; i++)
            {
                T value = _items[i];
                if (match(value))
                    oldItems.Add(i, value);
            }

            int result = _items.RemoveAll(match);

            if (result != oldItems.Count)
                throw new InvalidOperationException();

            if (result > 0)
            {
                OnCountChanged();
                OnItemsChanged();
                foreach (var oldItem in oldItems)
                    OnCollectionRemove(oldItem.Key, oldItem.Value);
            }

            return result;
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
            if (count == 0)
                return;

            T[] oldItems = GetRange(index, count).ToArray();

            _items.RemoveRange(index, count);

            OnCountChanged();
            OnItemsChanged();
            OnCollectionRemove(index, oldItems);
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
        public bool TrueForAll(Predicate<T> match)
        {
            return _items.TrueForAll(match);
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.List`1" /> to a new array.
        /// </summary>
        /// <returns>
        /// An array containing copies of the elements of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public T[] ToArray()
        {
            return _items.ToArray();
        }

        /// <summary>
        /// Sets the capacity to the actual number of elements in the <see cref="T:System.Collections.Generic.List`1" />, if that number is less than a threshold value.
        /// </summary>
        public void TrimExcess()
        {
            _items.TrimExcess();
        }

        /// <summary>
        /// Reverses the order of the elements in the entire <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        public void Reverse()
        {
            _items.Reverse();
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
            _items.Reverse(index, count);

            OnItemsChanged();
        }

        /// <summary>
        /// Sorts the elements in the entire <see cref="T:System.Collections.Generic.List`1" /> using the default comparer.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">
        /// The default comparer <see cref="P:System.Collections.Generic.Comparer`1.Default" /> cannot find an implementation of the <see cref="T:System.IComparable`1" /> generic interface or the <see cref="T:System.IComparable" /> interface for type T.
        /// </exception>
        public void Sort()
        {
            _items.Sort();

            OnItemsChanged();
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
            T[] array = _items.ToArray();

            Array.Sort(array, 0, array.Length, comparer);

            _items.Clear();
            _items.AddRange(array);

            OnItemsChanged();
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
        public void Sort(IComparer<T> comparer)
        {
            _items.Sort(comparer);

            OnItemsChanged();
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
        public void Sort(int index, int count, IComparer<T> comparer)
        {
            _items.Sort(index, count, comparer);

            OnItemsChanged();
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
        public void Sort(Comparison<T> comparison)
        {
            _items.Sort(comparison);

            OnItemsChanged();
        }

        #endregion List

        #region INotifyCollectionChanged

        /// <summary>
        /// Occurs when an item is added, removed, changed, moved, or the entire list is refreshed.
        /// </summary>
        [field: NonSerializedAttribute]
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with the provided arguments.
        /// </summary>
        /// <param name="startIndex">The start index.</param>
        /// <param name="newItems">The new items.</param>
        protected void OnCollectionAdd(int startIndex, params T[] newItems)
        {
            NotifyCollectionChangedEventHandler handler = CollectionChanged;
            if (handler == null)
                return;

            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItems, startIndex);
            handler(this, args);
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with the provided arguments.
        /// </summary>
        /// <param name="startIndex">The start index.</param>
        /// <param name="oldItems">The old items.</param>
        protected void OnCollectionRemove(int startIndex, params T[] oldItems)
        {
            NotifyCollectionChangedEventHandler handler = CollectionChanged;
            if (handler == null)
                return;

            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems, startIndex);
            handler(this, args);
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event with the provided arguments.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="oldItem">The old item.</param>
        /// <param name="newItem">The new item.</param>
        protected void OnCollectionReplace(int index, T oldItem, T newItem)
        {
            NotifyCollectionChangedEventHandler handler = CollectionChanged;
            if (handler == null)
                return;

            var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, oldItem, newItem, index);
            handler(this, args);
        }

        #endregion INotifyCollectionChanged

        #region INotifyPropertyChanged

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        [field: NonSerializedAttribute]
        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly PropertyChangedEventArgs _countChangedEventArgs = new PropertyChangedEventArgs("Count");
        private static readonly PropertyChangedEventArgs _itemChangedEventArgs = new PropertyChangedEventArgs("Item[]");

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the Count property.
        /// </summary>
        protected void OnCountChanged()
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null)
                return;

            handler(this, _countChangedEventArgs);
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the Item[] property.
        /// </summary>
        protected void OnItemsChanged()
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null)
                return;

            handler(this, _itemChangedEventArgs);
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event with the provided arguments.
        /// </summary>
        /// <param name="e">Arguments of the event being raised.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null)
                return;

            handler(this, e);
        }

        #endregion INotifyPropertyChanged
    }

    public interface IObservableList<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged { }
}
#endif
