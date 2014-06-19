using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Exceptionless.Extensions;

namespace Exceptionless.Storage {
    public class InMemoryFileStorage : IFileStorage {
        private readonly Dictionary<string, Tuple<FileInfo, string>> _storage = new Dictionary<string, Tuple<FileInfo, string>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public InMemoryFileStorage() : this(1024 * 1024 * 256, 100) {}

        public InMemoryFileStorage(long maxFileSize, int maxFiles) {
            MaxFileSize = maxFileSize;
            MaxFiles = maxFiles;
        }

        public long MaxFileSize { get; set; }
        public long MaxFiles { get; set; }

        public string GetFileContents(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            lock (_lock) {
                if (!_storage.ContainsKey(path))
                    throw new FileNotFoundException();

                return _storage[path].Item2;
            }
        }

        public bool Exists(string path) {
            return _storage.ContainsKey(path);
        }

        private static byte[] ReadBytes(Stream input) {
            using (var ms = new MemoryStream()) {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public bool SaveFile(string path, string contents) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            if (contents.Length > MaxFileSize)
                throw new ArgumentException(String.Format("File size {0} exceeds the maximum size of {1}.", contents.Length.ToFileSizeDisplay(), MaxFileSize.ToFileSizeDisplay()));

            lock (_lock) {
                _storage[path] = Tuple.Create(new FileInfo {
                    Created = DateTime.Now,
                    Modified = DateTime.Now,
                    Path = path,
                    Size = contents.Length
                }, contents);

                if (_storage.Count > MaxFiles)
                    _storage.Remove(_storage.OrderByDescending(kvp => kvp.Value.Item1.Created).First().Key);
            }

            return true;
        }

        public bool RenameFile(string oldpath, string newpath) {
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

        public bool DeleteFile(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            lock (_lock) {
                if (!_storage.ContainsKey(path))
                    return false;
                
                _storage.Remove(path);
            }

            return true;
        }

        public IEnumerable<FileInfo> GetFileList(string searchPattern = null) {
            if (searchPattern == null)
                searchPattern = "*";

            var regex = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*?") + "$");
            lock (_lock)
                return _storage.Keys.Where(k => regex.IsMatch(k)).Select(k => _storage[k].Item1).ToList();
        }

        public void Dispose() {
            if (_storage != null)
                _storage.Clear();
        }
    }
}