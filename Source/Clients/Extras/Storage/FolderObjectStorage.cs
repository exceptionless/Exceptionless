using System;
using System.Collections.Generic;
using System.IO;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Extras.Utility;
using Exceptionless.Storage;

namespace Exceptionless.Extras.Storage {
    public class FolderObjectStorage : IObjectStorage {
        private readonly object _lockObject = new object();
        private readonly IDependencyResolver _resolver;

        public FolderObjectStorage(IDependencyResolver resolver, string folder) {
            _resolver = resolver;

            folder = PathHelper.ExpandPath(folder);

            if (!Path.IsPathRooted(folder))
                folder = Path.GetFullPath(folder);
            if (!folder.EndsWith("\\"))
                folder += "\\";

            Folder = folder;

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        public string Folder { get; set; }

        public T GetObject<T>(string path) where T : class {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                var json = File.ReadAllText(Path.Combine(Folder, path));
                if (String.IsNullOrEmpty(json))
                    return null;

                var serializer = _resolver.GetJsonSerializer();
                return serializer.Deserialize<T>(json);
            } catch (Exception ex) {
                _resolver.GetLog().Error(ex.Message, exception: ex);
                return null;
            }
        }

        public ObjectInfo GetObjectInfo(string path) {
            var info = new System.IO.FileInfo(path);
            if (!info.Exists)
                return null;

            return new ObjectInfo {
                Path = path.Replace(Folder, String.Empty),
                Created = info.CreationTime,
                Modified = info.LastWriteTime
            };
        }

        public bool Exists(string path) {
            return File.Exists(Path.Combine(Folder, path));
        }

        public bool SaveObject<T>(string path, T value) where T : class {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            string directory = Path.GetDirectoryName(Path.Combine(Folder, path));
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try {
                var serializer = _resolver.GetJsonSerializer();
                string json = serializer.Serialize(value);
                File.WriteAllText(Path.Combine(Folder, path), json);
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
                    File.Move(Path.Combine(Folder, oldpath), Path.Combine(Folder, newpath));
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
                File.Delete(Path.Combine(Folder, path));
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public IEnumerable<ObjectInfo> GetObjectList(string searchPattern = null, int? limit = null, DateTime? maxCreatedDate = null) {
            if (String.IsNullOrEmpty(searchPattern))
                searchPattern = "*";

            if (!maxCreatedDate.HasValue)
                maxCreatedDate = DateTime.MaxValue;

            var list = new List<ObjectInfo>();

            foreach (var path in Directory.GetFiles(Folder, searchPattern, SearchOption.AllDirectories)) {
                var info = new System.IO.FileInfo(path);
                if (!info.Exists || info.CreationTime > maxCreatedDate)
                    continue;

                list.Add(new ObjectInfo {
                    Path = path.Replace(Folder, String.Empty),
                    Created = info.CreationTime,
                    Modified = info.LastWriteTime
                });

                if (list.Count == limit)
                    break;
            }

            return list;
        }

        public void Dispose() {}
    }
}