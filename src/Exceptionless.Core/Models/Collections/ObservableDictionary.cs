using System;
using System.Collections;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Collections {
    public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
        private readonly IDictionary<TKey, TValue> _dictionary;

        public ObservableDictionary() {
            _dictionary = new Dictionary<TKey, TValue>();
        }

        public ObservableDictionary(IDictionary<TKey, TValue> dictionary) {
            _dictionary = new Dictionary<TKey, TValue>(dictionary);
        }

        public ObservableDictionary(IEqualityComparer<TKey> comparer) {
            _dictionary = new Dictionary<TKey, TValue>(comparer);
        }

        public ObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) {
            _dictionary = new Dictionary<TKey, TValue>(dictionary, comparer);
        }

        public void Add(TKey key, TValue value) {
            _dictionary.Add(key, value);

            OnChanged(new ChangedEventArgs<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value), ChangedAction.Add));
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            _dictionary.Add(item);

            OnChanged(new ChangedEventArgs<KeyValuePair<TKey, TValue>>(item, ChangedAction.Add));
        }

        public bool Remove(TKey key) {
            bool success = _dictionary.Remove(key);

            if (success)
                OnChanged(new ChangedEventArgs<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, default), ChangedAction.Remove));

            return success;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            bool success = _dictionary.Remove(item);

            if (success)
                OnChanged(new ChangedEventArgs<KeyValuePair<TKey, TValue>>(item, ChangedAction.Remove));

            return success;
        }

        public void Clear() {
            _dictionary.Clear();

            OnChanged(new ChangedEventArgs<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(), ChangedAction.Clear));
        }

        public bool ContainsKey(TKey key) {
            return _dictionary.ContainsKey(key);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            return _dictionary.Contains(item);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            return _dictionary.TryGetValue(key, out value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            _dictionary.CopyTo(array, arrayIndex);
        }

        public ICollection<TKey> Keys => _dictionary.Keys;

        public ICollection<TValue> Values => _dictionary.Values;

        public int Count => _dictionary.Count;

        public bool IsReadOnly => _dictionary.IsReadOnly;

        public TValue this[TKey key] {
            get { return _dictionary[key]; }
            set {
                ChangedAction action = ContainsKey(key) ? ChangedAction.Update : ChangedAction.Add;

                _dictionary[key] = value;
                OnChanged(new ChangedEventArgs<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value), action));
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public event EventHandler<ChangedEventArgs<KeyValuePair<TKey, TValue>>> Changed;

        private void OnChanged(ChangedEventArgs<KeyValuePair<TKey, TValue>> args) {
            Changed?.Invoke(this, args);
        }
    }

    public class ChangedEventArgs<T> : EventArgs {
        public T Item { get; private set; }
        public ChangedAction Action { get; private set; }

        public ChangedEventArgs(T item, ChangedAction action) {
            Item = item;
            Action = action;
        }
    }

    public enum ChangedAction {
        Add,
        Remove,
        Clear,
        Update
    }
}
