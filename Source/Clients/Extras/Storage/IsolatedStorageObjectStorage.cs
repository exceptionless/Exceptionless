using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Storage;
using Exceptionless.Utility;

namespace Exceptionless.Extras.Storage {
    public class IsolatedStorageObjectStorage : IObjectStorage {
        private readonly object _lockObject = new object();
        private readonly IDependencyResolver _resolver;

        public IsolatedStorageObjectStorage(IDependencyResolver resolver) {
            _resolver = resolver;
        }

        private IsolatedStorageFile GetIsolatedStorage() {
            return Run.WithRetries(() => IsolatedStorageFile.GetStore(IsolatedStorageScope.Machine | IsolatedStorageScope.Assembly, typeof(IsolatedStorageObjectStorage), null));
        }

        public IEnumerable<string> GetObjects(string searchPattern = null, int? limit = null) {
            int count = 0;
            var stack = new Stack<string>();

            const string initialDirectory = "*";
            stack.Push(initialDirectory);
            Regex searchPatternRegex = null;
            if (!String.IsNullOrEmpty(searchPattern))
                searchPatternRegex = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*?") + "$");

            while (stack.Count > 0) {
                string dir = stack.Pop();

                string directoryPath;
                if (dir == "*")
                    directoryPath = "*";
                else
                    directoryPath = dir + @"\*";

                string[] files, directories;
                using (var store = GetIsolatedStorage()) {
                    files = store.GetFileNames(directoryPath);
                    directories = store.GetDirectoryNames(directoryPath);
                }

                foreach (string file in files) {
                    string fullPath = dir != "*" ? Path.Combine(dir, file) : file;
                    if (searchPatternRegex != null && !searchPatternRegex.IsMatch(fullPath))
                        continue;

                    yield return fullPath;
                    count++;

                    if (limit.HasValue && count >= limit)
                        yield break;
                }

                foreach (string directoryName in directories)
                    stack.Push(dir == "*" ? directoryName : Path.Combine(dir, directoryName));
            }
        }

        public ObjectInfo GetObjectInfo(string path) {
            if (!Exists(path))
                return null;

            DateTime createdDate, modifiedDate;
            using (var store = GetIsolatedStorage()) {
                createdDate = store.GetCreationTime(path).LocalDateTime;
                modifiedDate = store.GetLastWriteTime(path).LocalDateTime;
            }

            return new ObjectInfo {
                Path = path,
                Modified = modifiedDate,
                Created = createdDate
            };
        }

        public bool Exists(string path) {
            return GetObjects(path).Any();
        }

        public T GetObject<T>(string path) where T : class {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                var json = Run.WithRetries(() => {
                    using (var store = GetIsolatedStorage()) {
                        using (var stream = new IsolatedStorageFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, store)) {
                            using (var reader = new StreamReader(stream)) {
                                return reader.ReadToEnd();
                            }
                        }
                    }
                });

                if (String.IsNullOrEmpty(json))
                    return null;

                var serializer = _resolver.GetJsonSerializer();
                return serializer.Deserialize<T>(json);
            } catch (Exception ex) {
                _resolver.GetLog().Error(ex.Message, exception: ex);
                return null;
            }
        }

        public bool SaveObject<T>(string path, T value) where T : class {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            EnsureDirectory(path);

            string json;
            try {
                var serializer = _resolver.GetJsonSerializer();
                json = serializer.Serialize(value);
            } catch (Exception ex) {
                _resolver.GetLog().Error(ex.Message, exception: ex);
                return false;
            }

            try {
                lock (_lockObject) {
                    Run.WithRetries(() => {
                        using (var store = GetIsolatedStorage()) {
                            using (var stream = new IsolatedStorageFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, store)) {
                                using (var streamWriter = new StreamWriter(stream)) {
                                    streamWriter.Write(json);
                                }
                            }
                        }
                    });
                }
            } catch (Exception ex) {
                _resolver.GetLog().Error(ex.Message, exception: ex);
                return false;
            }

            return true;
        }

        public bool RenameObject(string oldpath, string newpath) {
            if (String.IsNullOrWhiteSpace(oldpath))
                throw new ArgumentNullException("oldpath");
            if (String.IsNullOrWhiteSpace(newpath))
                throw new ArgumentNullException("newpath");

            try {
                lock (_lockObject) {
                    Run.WithRetries(() => {
                        using (var store = GetIsolatedStorage())
                            store.MoveFile(oldpath, newpath);
                    });
                }
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public bool DeleteObject(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                lock (_lockObject) {
                    Run.WithRetries(() => {
                        using (var store = GetIsolatedStorage())
                            store.DeleteFile(path);
                    });
                }
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public IEnumerable<ObjectInfo> GetObjectList(string searchPattern = null, int? limit = null, DateTime? maxCreatedDate = null) {
            int count = 0;
            if (!maxCreatedDate.HasValue)
                maxCreatedDate = DateTime.MaxValue;

            foreach (string path in GetObjects(searchPattern)) {
                ObjectInfo info = GetObjectInfo(path);

                if (info == null || info.Created > maxCreatedDate)
                    continue;

                yield return info;
                count++;

                if (limit.HasValue && count >= limit)
                    yield break;

            }
        }

        private readonly Collection<string> _ensuredDirectories = new Collection<string>();

        private void EnsureDirectory(string path) {
            string directory = Path.GetDirectoryName(path);
            if (String.IsNullOrEmpty(directory))
                return;

            if (_ensuredDirectories.Contains(directory))
                return;

            Run.WithRetries(() => {
                using (var store = GetIsolatedStorage()) {
                    if (!store.DirectoryExists(directory))
                        store.CreateDirectory(directory);
                    _ensuredDirectories.Add(directory);
                }
            });
        }
       
        public void Dispose() {
            GC.SuppressFinalize(this);
        }
    }

    internal static class IsolatedFileStoreExtensions {
        public static string GetFullPath(this IsolatedStorageFile storage, string path = null) {
            try {
                FieldInfo field = storage.GetType().GetField("m_RootDir", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) {
                    string directory = field.GetValue(storage).ToString();
                    return String.IsNullOrEmpty(path) ? directory : Path.Combine(Path.GetFullPath(directory), path);
                }
            } catch { }
            return null;
        }
    }
}
