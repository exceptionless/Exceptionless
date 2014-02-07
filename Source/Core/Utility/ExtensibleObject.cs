#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using CodeSmith.Core.Extensions;
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
            object value = _extendedData[name];

            if (value == null)
                throw new InvalidOperationException(String.Format("Property value \"{0}\" is null.  Can't use generic method on null values.", name));

            if (value is T)
                return (T)value;

            if (value is JContainer)
                return ((JContainer)value).ToObject<T>();

            return value.ToType<T>();
        }

        public object GetProperty(string name) {
            return _extendedData[name];
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
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}