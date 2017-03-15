using System;
using System.Collections.Generic;
using System.ComponentModel;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Utility {
    public interface IExtensibleObject {
        void SetProperty<T>(string name, T value);

        T GetProperty<T>(string name);

        object GetProperty(string name);

        bool HasProperty(string name);

        void RemoveProperty(string name);

        IEnumerable<KeyValuePair<string, object>> GetProperties();
    }

    public class ExtensibleObject : INotifyPropertyChanged, IExtensibleObject {
        public ExtensibleObject() {
            _extendedData = new Dictionary<string, object>();
        }

        [JsonProperty]
        private readonly Dictionary<string, object> _extendedData;

        public void SetProperty<T>(string name, T value) {
            if (_extendedData.ContainsKey(name))
                _extendedData[name] = value;
            else
                _extendedData.Add(name, value);

            NotifyPropertyChanged(name);
        }

        public T GetProperty<T>(string name) {
            object value = GetProperty(name);
            if (value == null)
                throw new InvalidOperationException($"Property value \"{name}\" is null.  Can't use generic method on null values.");

            if (value is T)
                return (T)value;

            if (value is JContainer)
                return ((JContainer)value).ToObject<T>();

            return value.ToType<T>();
        }

        public object GetProperty(string name) {
            return _extendedData.TryGetValue(name, out object value) ? value : null;
        }

        public IEnumerable<KeyValuePair<string, object>> GetProperties() {
            return _extendedData;
        }

        public bool HasProperty(string name) {
            return _extendedData.ContainsKey(name);
        }

        public void RemoveProperty(string name) {
            if (_extendedData.ContainsKey(name)) {
                _extendedData.Remove(name);
                NotifyPropertyChanged(name);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}