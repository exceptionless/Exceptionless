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
    public class WrappedObservableCollection<TBase, TActual> : IObservableCollection<TBase>
        where TBase : class
        where TActual : class, TBase
    {
        protected readonly System.Collections.ObjectModel.ObservableCollection<TActual> _innerCollection;

        public WrappedObservableCollection()
            : this(null)
        { }

        public WrappedObservableCollection(IEnumerable<TBase> data)
            : this(data == null ? null : data.Cast<TActual>())
        { }

        public WrappedObservableCollection(IEnumerable<TActual> data)
        {
            _innerCollection = data == null ?
                new System.Collections.ObjectModel.ObservableCollection<TActual>()
                : new System.Collections.ObjectModel.ObservableCollection<TActual>(data);
            _innerCollection.CollectionChanged += InnerCollectionChanged;
            ((INotifyPropertyChanged)_innerCollection).PropertyChanged += InnerCollectionPropertyChanged;
        }

        #region ICollection<T>

        /// <summary>
        /// Gets the number of elements actually contained in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <returns>
        /// The number of elements actually contained in the <see cref="T:System.Collections.Generic.List`1" />.
        /// </returns>
        public int Count
        {
            get { return _innerCollection.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
        /// </summary>
        /// <returns><c>true</c> if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, <c>false</c>.
        ///   </returns>
        bool ICollection<TBase>.IsReadOnly
        {
            get { return ((ICollection<TBase>)_innerCollection).IsReadOnly; }
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <param name="item">
        /// The object to be added to the end of the <see cref="T:System.Collections.Generic.List`1" />. The value can be <c>null</c> for reference types.
        /// </param>
        public void Add(TBase item)
        {
            _innerCollection.Add((TActual)item);
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        public void Clear()
        {
            _innerCollection.Clear();
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
            return _innerCollection.Contains(item);
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
            _innerCollection.CopyTo((TActual[])array, arrayIndex);
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
            return _innerCollection.Remove((TActual)item);
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
            return ((IEnumerable<TBase>)_innerCollection).GetEnumerator();
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
            return ((IEnumerable)_innerCollection).GetEnumerator();
        }

        #endregion IEnumerable

        #region INotifyCollectionChanged

        /// <summary>
        /// Occurs when an item is added, removed, changed, moved, or the entire list is refreshed.
        /// </summary>
        [field: NonSerializedAttribute]
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private void InnerCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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

        private void InnerCollectionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(sender, e);
        }

        #endregion INotifyPropertyChanged
    }
#endif

    public interface IObservableCollection<T> : ICollection<T>, INotifyCollectionChanged, INotifyPropertyChanged {}
}
