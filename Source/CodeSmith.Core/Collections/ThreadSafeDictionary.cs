using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;
using System.Xml.Serialization;

namespace CodeSmith.Core.Collections
{
    [XmlRoot("dictionary")]
    [Obsolete("Use ConcurrentDictionary instead.")]
    public class ThreadSafeDictionary<TKey, TValue>
        : IDictionary<TKey, TValue>,
          IDictionary,
          ISerializable,
          IDeserializationCallback,
          IXmlSerializable,
          INotifyPropertyChanged
    {
        protected readonly Dictionary<TKey, TValue> innerDictionary;
        private readonly ReaderWriterLockSlim _lock;
        private object _syncRoot;
        private const string COUNT_STRING = "Count";
        private const string INDEXER_NAME = "Item[]";

        #region Events
        /// <summary>
        /// Occurs when a property value changed.
        /// </summary>
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

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
        protected virtual void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Occurs when the collection changed.
        /// </summary>
        [field: NonSerialized]
        public event EventHandler CollectionChanged;

        /// <summary>
        /// Raises the CollectionChanged event with the provided arguments.
        /// </summary>
        protected virtual void OnCollectionChanged()
        {
            if (CollectionChanged != null)
                CollectionChanged(this, EventArgs.Empty);
        } 
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"/> class.
        /// </summary>
        public ThreadSafeDictionary()
        {
            innerDictionary = new Dictionary<TKey, TValue>();
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>Initializes a new instance of the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> class that contains elements copied from the specified <see cref="T:System.Collections.Generic.IDictionary`2"></see> and uses the default equality comparer for the key type.</summary>
        /// <param name="dictionary">The <see cref="T:System.Collections.Generic.IDictionary`2"></see> whose elements are copied to the new <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</param>
        /// <exception cref="T:System.ArgumentException">dictionary contains one or more duplicate keys.</exception>
        /// <exception cref="T:System.ArgumentNullException">dictionary is null.</exception>
        public ThreadSafeDictionary(IDictionary<TKey, TValue> dictionary)
        {
            innerDictionary = new Dictionary<TKey, TValue>(dictionary);
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>Initializes a new instance of the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> class that is empty, has the default initial capacity, and uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see>.</summary>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see> implementation to use when comparing keys, or null to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1"></see> for the type of the key.</param>
        public ThreadSafeDictionary(IEqualityComparer<TKey> comparer)
        {
            innerDictionary = new Dictionary<TKey, TValue>(comparer);
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>Initializes a new instance of the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> class that is empty, has the specified initial capacity, and uses the default equality comparer for the key type.</summary>
        /// <param name="capacity">The initial number of elements that the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> can contain.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">capacity is less than 0.</exception>
        public ThreadSafeDictionary(int capacity)
        {
            innerDictionary = new Dictionary<TKey, TValue>(capacity);
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>Initializes a new instance of the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> class that contains elements copied from the specified <see cref="T:System.Collections.Generic.IDictionary`2"></see> and uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see>.</summary>
        /// <param name="dictionary">The <see cref="T:System.Collections.Generic.IDictionary`2"></see> whose elements are copied to the new <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</param>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see> implementation to use when comparing keys, or null to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1"></see> for the type of the key.</param>
        /// <exception cref="T:System.ArgumentException">dictionary contains one or more duplicate keys.</exception>
        /// <exception cref="T:System.ArgumentNullException">dictionary is null.</exception>
        public ThreadSafeDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            innerDictionary = new Dictionary<TKey, TValue>(dictionary, comparer);
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>Initializes a new instance of the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> class that is empty, has the specified initial capacity, and uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see>.</summary>
        /// <param name="capacity">The initial number of elements that the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> can contain.</param>
        /// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see> implementation to use when comparing keys, or null to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1"></see> for the type of the key.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">capacity is less than 0.</exception>
        public ThreadSafeDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            innerDictionary = new Dictionary<TKey, TValue>(capacity, comparer);
            _lock = new ReaderWriterLockSlim();
        }

        #endregion

        /// <summary>Gets the <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see> that is used to determine equality of keys for the dictionary. </summary>
        /// <returns>The <see cref="T:System.Collections.Generic.IEqualityComparer`1"></see> generic interface implementation that is used to determine equality of keys for the current <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> and to provide hash values for the keys.</returns>
        public IEqualityComparer<TKey> Comparer
        {
            get { return innerDictionary.Comparer; }
        }

        /// <summary>Gets a collection containing the keys in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</summary>
        /// <returns>A <see cref="Dictionary{TKey,TValue}.KeyCollection"></see> containing the keys in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</returns>
        public Dictionary<TKey, TValue>.KeyCollection Keys
        {
            get
            {
                using (EnterReadLock())
                    return innerDictionary.Keys;
            }
        }

        /// <summary>Gets a collection containing the values in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</summary>
        /// <returns>A <see cref="Dictionary{TKey,TValue}.ValueCollection"></see> containing the values in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</returns>
        public Dictionary<TKey, TValue>.ValueCollection Values
        {
            get
            {
                using (EnterReadLock())
                    return innerDictionary.Values;
            }
        }

        #region IDeserializationCallback Members

        /// <summary>Implements the <see cref="T:System.Runtime.Serialization.ISerializable"></see> interface and raises the deserialization event when the deserialization is complete.</summary>
        /// <param name="sender">The source of the deserialization event.</param>
        /// <exception cref="T:System.Runtime.Serialization.SerializationException">The <see cref="T:System.Runtime.Serialization.SerializationInfo"></see> object associated with the current <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> instance is invalid.</exception>
        public virtual void OnDeserialization(object sender)
        {
            innerDictionary.OnDeserialization(sender);
        }

        #endregion

        #region IDictionary Members

        void ICollection.CopyTo(Array array, int index)
        {
            using (EnterReadLock())
                ((ICollection)innerDictionary).CopyTo(array, index);
        }

        void IDictionary.Add(object key, object value)
        {
            using (EnterWriteLock())
                ((IDictionary)innerDictionary).Add(key, value);
        }

        bool IDictionary.Contains(object key)
        {
            using (EnterReadLock())
                return ((IDictionary)innerDictionary).Contains(key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)innerDictionary).GetEnumerator();
        }

        void IDictionary.Remove(object key)
        {
            using (EnterWriteLock())
                ((IDictionary)innerDictionary).Remove(key);
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                return _syncRoot;
            }
        }

        bool IDictionary.IsFixedSize
        {
            get { return false; }
        }

        bool IDictionary.IsReadOnly
        {
            get { return false; }
        }

        object IDictionary.this[object key]
        {
            get
            {
                using (EnterReadLock())
                    return ((IDictionary)innerDictionary)[key];
            }
            set
            {
                using (EnterWriteLock())
                    ((IDictionary)innerDictionary)[key] = value;
            }
        }

        ICollection IDictionary.Keys
        {
            get
            {
                using (EnterReadLock())
                    return ((IDictionary)innerDictionary).Keys;
            }
        }

        ICollection IDictionary.Values
        {
            get
            {
                using (EnterReadLock())
                    return ((IDictionary)innerDictionary).Values;
            }
        }

        #endregion

        #region IDictionary<TKey,TValue> Members

        /// <summary>Adds the specified key and value to the dictionary.</summary>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <exception cref="T:System.ArgumentException">An element with the same key already exists in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</exception>
        /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
        public void Add(TKey key, TValue value)
        {
            using (EnterWriteLock())
                innerDictionary.Add(key, value);

            OnPropertyChanged(COUNT_STRING);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        /// <summary>Removes all keys and values from the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</summary>
        public void Clear()
        {
            using (EnterWriteLock())
                innerDictionary.Clear();

            OnPropertyChanged(COUNT_STRING);
            OnPropertyChanged(INDEXER_NAME);
            OnCollectionChanged();
        }

        /// <summary>Determines whether the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> contains the specified key.</summary>
        /// <returns>true if the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> contains an element with the specified key; otherwise, false.</returns>
        /// <param name="key">The key to locate in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</param>
        /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
        public bool ContainsKey(TKey key)
        {
            using (EnterReadLock())
                return innerDictionary.ContainsKey(key);
        }


        /// <summary>Removes the value with the specified key from the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</summary>
        /// <returns>true if the element is successfully found and removed; otherwise, false.  This method returns false if key is not found in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</returns>
        /// <param name="key">The key of the element to remove.</param>
        /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
        public bool Remove(TKey key)
        {
            bool result;
            using (EnterWriteLock())
                result = innerDictionary.Remove(key);

            if (result)
            {
                OnPropertyChanged(COUNT_STRING);
                OnPropertyChanged(INDEXER_NAME);
                OnCollectionChanged();
            }

            return result;
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
        {
            Add(keyValuePair.Key, keyValuePair.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
        {
            using (EnterReadLock())
                return ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).Contains(keyValuePair);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            using (EnterReadLock())
                ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).CopyTo(array, index);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
        {
            return Remove(keyValuePair.Key);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)innerDictionary).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)innerDictionary).GetEnumerator();
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter. This parameter is passed uninitialized.</param>
        /// <returns>
        /// true if the object that implements <see cref="T:System.Collections.Generic.IDictionary`2"/> contains an element with the specified key; otherwise, false.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="key"/> is null.</exception>
        public bool TryGetValue(TKey key, out TValue value)
        {
            using (EnterReadLock())
                return innerDictionary.TryGetValue(key, out value);
        }

        /// <summary>Gets the number of key/value pairs contained in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</summary>
        /// <returns>The number of key/value pairs contained in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</returns>
        public int Count
        {
            get
            {
                using (EnterReadLock())
                    return innerDictionary.Count;
            }
        }

        /// <summary>Gets or sets the value associated with the specified key.</summary>
        /// <returns>The value associated with the specified key. If the specified key is not found, a get operation throws a <see cref="T:System.Collections.Generic.KeyNotFoundException"></see>, and a set operation creates a new element with the specified key.</returns>
        /// <param name="key">The key of the value to get or set.</param>
        /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
        /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The property is retrieved and key does not exist in the collection.</exception>
        public TValue this[TKey key]
        {
            get
            {
                using (EnterReadLock())
                    return innerDictionary[key];
            }
            set
            {
                using (EnterWriteLock())
                    innerDictionary[key] = value;

                OnPropertyChanged(INDEXER_NAME);
                OnCollectionChanged();
            }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
        {
            get { return false; }
        }


        ICollection<TKey> IDictionary<TKey, TValue>.Keys
        {
            get
            {
                using (EnterReadLock())
                    return ((IDictionary<TKey, TValue>)innerDictionary).Keys;
            }
        }

        ICollection<TValue> IDictionary<TKey, TValue>.Values
        {
            get
            {
                using (EnterReadLock())
                    return ((IDictionary<TKey, TValue>)innerDictionary).Values;
            }
        }

        #endregion

        #region ISerializable Members

        /// <summary>Implements the <see cref="T:System.Runtime.Serialization.ISerializable"></see> interface and returns the data needed to serialize the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> instance.</summary>
        /// <param name="context">A <see cref="T:System.Runtime.Serialization.StreamingContext"></see> structure that contains the source and destination of the serialized stream associated with the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> instance.</param>
        /// <param name="info">A <see cref="T:System.Runtime.Serialization.SerializationInfo"></see> object that contains the information required to serialize the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> instance.</param>
        /// <exception cref="T:System.ArgumentNullException">info is null.</exception>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            innerDictionary.GetObjectData(info, context);
        }

        #endregion

        /// <summary>Determines whether the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> contains a specific value.</summary>
        /// <returns>true if the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see> contains an element with the specified value; otherwise, false.</returns>
        /// <param name="value">The value to locate in the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>. The value can be null for reference types.</param>
        public bool ContainsValue(TValue value)
        {
            using (EnterReadLock())
                return innerDictionary.ContainsValue(value);
        }

        /// <summary>Returns an enumerator that iterates through the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</summary>
        /// <returns>A <see cref="Dictionary{TKey,TValue}.Enumerator"></see> structure for the <see cref="ThreadSafeDictionary&lt;TKey, TValue&gt;"></see>.</returns>
        public Dictionary<TKey, TValue>.Enumerator GetEnumerator()
        {
            return innerDictionary.GetEnumerator();
        }

        #region IXmlSerializable Members
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty)
                return;

            using (EnterWriteLock())
            {
                while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
                {
                    reader.ReadStartElement("item");
                    reader.ReadStartElement("key");
                    TKey key = (TKey)keySerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    reader.ReadStartElement("value");
                    TValue value = (TValue)valueSerializer.Deserialize(reader);
                    reader.ReadEndElement();

                    innerDictionary.Add(key, value);

                    reader.ReadEndElement();
                    reader.MoveToContent();
                }
            }

            reader.ReadEndElement();
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {

            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            using (EnterReadLock())
            {
                foreach (TKey key in innerDictionary.Keys)
                {
                    writer.WriteStartElement("item");

                    writer.WriteStartElement("key");
                    keySerializer.Serialize(writer, key);
                    writer.WriteEndElement();

                    writer.WriteStartElement("value");
                    TValue value = innerDictionary[key];
                    valueSerializer.Serialize(writer, value);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }
            }
        }
        #endregion

        #region Disposable Lock Class
        /// <summary>
        /// Enters the read lock.
        /// </summary>
        /// <returns></returns>
        public IDisposable EnterReadLock()
        {
            _lock.TryEnterReadLock(500);
            return new DisposableAction(_lock.ExitReadLock);
        }

        /// <summary>
        /// Enters the upgradeable read lock.
        /// </summary>
        /// <returns></returns>
        public IDisposable EnterUpgradeableReadLock()
        {
            _lock.TryEnterUpgradeableReadLock(500);
            return new DisposableAction(_lock.ExitUpgradeableReadLock);
        }

        /// <summary>
        /// Enters the write lock.
        /// </summary>
        /// <returns></returns>
        public IDisposable EnterWriteLock()
        {
            _lock.TryEnterWriteLock(500);
            return new DisposableAction(_lock.ExitWriteLock);
        }

        private class DisposableAction : IDisposable
        {
            private readonly Action _exitAction;

            public DisposableAction(Action exitAction)
            {
                _exitAction = exitAction;
            }

            void IDisposable.Dispose()
            {
                _exitAction.Invoke();
            }
        }
        #endregion

    }
}
