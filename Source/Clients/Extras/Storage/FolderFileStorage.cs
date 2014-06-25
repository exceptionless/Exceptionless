using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exceptionless.Extras.Utility;
using Exceptionless.Storage;
using FileInfo = Exceptionless.Storage.FileInfo;

namespace Exceptionless.Extras.Storage {
    public class FolderFileStorage : IFileStorage {
        private readonly object _lockObject = new object();

        public FolderFileStorage(string folder) {
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

        public string GetFileContents(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                return File.ReadAllText(Path.Combine(Folder, path));
            } catch (Exception) {
                return null;
            }
        }

        public bool Exists(string path) {
            return File.Exists(Path.Combine(Folder, path));
        }

        public bool SaveFile(string path, string contents) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            string directory = Path.GetDirectoryName(Path.Combine(Folder, path));
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try {
                File.WriteAllText(Path.Combine(Folder, path), contents);
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public bool RenameFile(string oldpath, string newpath) {
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

        public bool DeleteFile(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                File.Delete(Path.Combine(Folder, path));
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public IEnumerable<FileInfo> GetFileList(string searchPattern = null, int? limit = null) {
            if (String.IsNullOrEmpty(searchPattern))
                searchPattern = "*";

            var list = new List<FileInfo>();

            foreach (var path in Directory.GetFiles(Folder, searchPattern, SearchOption.AllDirectories).Take(limit ?? Int32.MaxValue)) {
                var info = new System.IO.FileInfo(path);
                if (!info.Exists)
                    continue;

                list.Add(new FileInfo {
                    Path = path.Replace(Folder, String.Empty),
                    Created = info.CreationTime,
                    Modified = info.LastWriteTime,
                    Size = info.Length
                });
            }

            return list;
        }

        public void Dispose() {}
    }
}
