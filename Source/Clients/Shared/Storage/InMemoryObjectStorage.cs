using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Exceptionless.Storage {
    public class InMemoryObjectStorage : IObjectStorage {
        private readonly Dictionary<string, Tuple<ObjectInfo, object>> _storage = new Dictionary<string, Tuple<ObjectInfo, object>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public InMemoryObjectStorage() : this(100) {}

        public InMemoryObjectStorage(int maxObjects) {
            MaxObjects = maxObjects;
        }

        public long MaxObjects { get; set; }

        public T GetObject<T>(string path) where T : class {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            lock (_lock) {
                if (!_storage.ContainsKey(path))
                    throw new FileNotFoundException();

                return _storage[path].Item2 as T;
            }
        }

        public ObjectInfo GetObjectInfo(string path) {
            return Exists(path) ? _storage[path].Item1 : null;
        }

        public bool Exists(string path) {
            return _storage.ContainsKey(path);
        }

        public bool SaveObject<T>(string path, T value) where T : class {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            lock (_lock) {
                _storage[path] = Tuple.Create(new ObjectInfo {
                    Created = DateTime.Now,
                    Modified = DateTime.Now,
                    Path = path
                }, (object)value);

                if (_storage.Count > MaxObjects)
                    _storage.Remove(_storage.OrderByDescending(kvp => kvp.Value.Item1.Created).First().Key);
            }

            return true;
        }

        public bool RenameObject(string oldpath, string newpath) {
            if (String.IsNullOrWhiteSpace(oldpath))
                throw new ArgumentNullException("oldpath");
            if (String.IsNullOrWhiteSpace(newpath))
                throw new ArgumentNullException("newpath");

            lock (_lock) {
                if (!_storage.ContainsKey(oldpath))
                    return false;

                _storage[newpath] = _storage[oldpath];
                _storage[newpath].Item1.Path = newpath;
                _storage[newpath].Item1.Modified = DateTime.Now;
                _storage.Remove(oldpath);
            }

            return true;
        }

        public bool DeleteObject(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            lock (_lock) {
                if (!_storage.ContainsKey(path))
                    return false;
                
                _storage.Remove(path);
            }

            return true;
        }

        public IEnumerable<ObjectInfo> GetObjectList(string searchPattern = null, int? limit = null, DateTime? maxCreatedDate = null) {
            if (searchPattern == null)
                searchPattern = "*";
            if (!maxCreatedDate.HasValue)
                maxCreatedDate = DateTime.MaxValue;

            var regex = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*?") + "$");
            lock (_lock)
                return _storage.Keys.Where(k => regex.IsMatch(k)).Select(k => _storage[k].Item1).Where(f => f.Created <= maxCreatedDate).Take(limit ?? Int32.MaxValue).ToList();
        }

        public void Dispose() {
            if (_storage != null)
                _storage.Clear();
        }
    }
}