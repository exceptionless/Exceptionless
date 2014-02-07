using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CodeSmith.Core.Collections
{
    /// <summary>
    /// A collection that provides notifications when items get added, removed, or when the whole list is refreshed. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
#if !SILVERLIGHT
    [Serializable]
#endif
    public class ObservableCollection<T> : Collection<T>, INotifyPropertyChanged
    {
        private const string COUNT_STRING = "Count";
        private const string INDEXER_NAME = "Item[]";
        private readonly SimpleMonitor _monitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCollection&lt;T&gt;"/> class.
        /// </summary>
        public ObservableCollection()
        {
            _monitor = new SimpleMonitor();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCollection&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public ObservableCollection(IEnumerable<T> collection)
        {
            _monitor = new SimpleMonitor();
            if (collection == null)
                throw new ArgumentNullException("collection");
            CopyFrom(collection);
        }

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Occurs when a property value changed.
        /// </summary>
#if !SILVERLIGHT
        [field: NonSerialized]
#endif
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        /// <summary>
        /// Occurs when the collection changed.
        /// </summary>
#if !SILVERLIGHT
        [field: NonSerialized]
#endif
        public event EventHandler CollectionChanged;

        /// <summary>
        /// Disallows reentrant attempts to change this collection.
        /// </summary>
        /// <returns>An IDisposable object that can be used to dispose of the object.</returns>
        protected IDisposable BlockReentrancy()
        {
            _monitor.Enter();
            return _monitor;
        }

        /// <summary>
        /// Checks for reentrant attempts to change this collection.
        /// </summary>
        protected void CheckReentrancy()
        {
            if ((_monitor.Busy && (CollectionChanged != null)) && (CollectionChanged.GetInvocationList().Length > 1))
                throw new InvalidOperationException("ObservableCollectionReentrancyNotAllowed");
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.ObjectModel.Collection`1"/>.
        /// </summary>
        protected override void ClearItems()
        {
            CheckReentrancy();
            base.ClearItems();
            OnPropertyChanged(COUNT_STRING);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        private void CopyFrom(IEnumerable<T> collection)
        {
            IList<T> items = base.Items;
            if ((collection != null) && (items != null))
                using (IEnumerator<T> enumerator = collection.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                        items.Add(enumerator.Current);
                }
        }

        /// <summary>
        /// Inserts an element into the <see cref="T:System.Collections.ObjectModel.Collection`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The object to insert. The value can be null for reference types.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="index"/> is less than zero.
        /// -or-
        /// <paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void InsertItem(int index, T item)
        {
            CheckReentrancy();
            base.InsertItem(index, item);
            OnPropertyChanged(COUNT_STRING);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        /// <summary>
        /// Moves the item at the specified index to a new location in the collection.
        /// </summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        public void Move(int oldIndex, int newIndex)
        {
            MoveItem(oldIndex, newIndex);
        }

        /// <summary>
        /// Moves the item at the specified index to a new location in the collection.
        /// </summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        protected virtual void MoveItem(int oldIndex, int newIndex)
        {
            CheckReentrancy();
            T item = base[oldIndex];
            base.RemoveItem(oldIndex);
            base.InsertItem(newIndex, item);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        /// <summary>
        /// Raises the CollectionChanged event with the provided arguments.
        /// </summary>
        protected virtual void OnCollectionChanged()
        {
            if (CollectionChanged != null)
                using (BlockReentrancy())
                {
                    CollectionChanged(this, EventArgs.Empty);
                }
        }

        /// <summary>
        /// Raises the PropertyChanged event with the provided arguments.
        /// </summary>
        /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        /// <summary>
        /// Raises the PropertyChanged event with the provided arguments.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="T:System.Collections.ObjectModel.Collection`1"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="index"/> is less than zero.
        /// -or-
        /// <paramref name="index"/> is equal to or greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void RemoveItem(int index)
        {
            CheckReentrancy();
            T item = base[index];
            base.RemoveItem(index);
            OnPropertyChanged(COUNT_STRING);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        /// <summary>
        /// Replaces the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to replace.</param>
        /// <param name="item">The new value for the element at the specified index. The value can be null for reference types.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="index"/> is less than zero.
        /// -or-
        /// <paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void SetItem(int index, T item)
        {
            CheckReentrancy();
            T oldItem = base[index];
            base.SetItem(index, item);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        #region Nested type: SimpleMonitor

#if !SILVERLIGHT
        [Serializable]
#endif
        private class SimpleMonitor : IDisposable
        {
            private int _busyCount;

            public bool Busy
            {
                get { return (_busyCount > 0); }
            }

            #region IDisposable Members

            public void Dispose()
            {
                _busyCount--;
            }

            #endregion

            public void Enter()
            {
                _busyCount++;
            }
        }

        #endregion
    }
}