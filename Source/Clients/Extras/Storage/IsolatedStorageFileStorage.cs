using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Exceptionless.Storage;
using Exceptionless.Utility;
using FileInfo = Exceptionless.Storage.FileInfo;

namespace Exceptionless.Extras.Storage {
    public class IsolatedStorageFileStorage : IFileStorage {
        private readonly object _lockObject = new object();

        private IsolatedStorageFile GetIsolatedStorage() {
            return Run.WithRetries(() => IsolatedStorageFile.GetStore(IsolatedStorageScope.Machine | IsolatedStorageScope.Assembly, typeof(IsolatedStorageFileStorage), null));
        }

        private long GetFileSize(IsolatedStorageFile store, string path) {
            string fullPath = store.GetFullPath(path);
            try {
                if (File.Exists(fullPath))
                    return new System.IO.FileInfo(fullPath).Length;
            } catch (IOException ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error getting size of file: {0}", ex.Message);
            }

            return -1;
        }

        public IEnumerable<string> GetFiles(string searchPattern = null, int? limit = null) {
            var result = new List<string>();
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

                using (var store = GetIsolatedStorage()) {
                    var filesInCurrentDirectory = store.GetFileNames(directoryPath).Take(limit ?? Int32.MaxValue).ToList();
                    var filesInCurrentDirectoryWithFolderName = filesInCurrentDirectory.Select(file => Path.Combine(dir, file)).ToList();
                    if (dir != "*")
                        result.AddRange(searchPatternRegex != null ? filesInCurrentDirectoryWithFolderName.Where(k => searchPatternRegex.IsMatch(k)) : filesInCurrentDirectoryWithFolderName);
                    else
                        result.AddRange(searchPatternRegex != null ? filesInCurrentDirectory.Where(k => searchPatternRegex.IsMatch(k)) : filesInCurrentDirectory);

                    if (limit.HasValue && result.Count >= limit)
                        return result;

                    foreach (string directoryName in store.GetDirectoryNames(directoryPath))
                        stack.Push(dir == "*" ? directoryName : Path.Combine(dir, directoryName));
                }
            }

            return result;
        }

        public FileInfo GetFileInfo(string path) {
            if (!Exists(path))
                return null;

            using (var store = GetIsolatedStorage()) {
                return new FileInfo {
                    Path = path,
                    Modified = store.GetLastWriteTime(path).LocalDateTime,
                    Created = store.GetCreationTime(path).LocalDateTime,
                    Size = GetFileSize(store, path)
                };
            }
        }

        public bool Exists(string path) {
            return GetFiles(path).Any();
        }

        public string GetFileContents(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                return Run.WithRetries(() => {
                    using (var store = GetIsolatedStorage())
                    using (var stream = new IsolatedStorageFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, store))
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                });
            } catch (Exception) {}

            return null;
        }

        public bool SaveFile(string path, string contents) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            EnsureDirectory(path);

            try {
                lock (_lockObject) {
                    Run.WithRetries(() => {
                        using (var store = GetIsolatedStorage())
                        using (var stream = new IsolatedStorageFileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, store))
                        using (var streamWriter = new StreamWriter(stream))
                            streamWriter.Write(contents);
                    });
                }
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
                    Run.WithRetries(() => {
                        using (var store = GetIsolatedStorage()) {
                            store.MoveFile(oldpath, newpath);
                        }
                    });
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
                lock (_lockObject) {
                    Run.WithRetries(() => {
                        using (var store = GetIsolatedStorage()) {
                            store.DeleteFile(path);
                        }
                    });
                }
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public IEnumerable<FileInfo> GetFileList(string searchPattern = null, int? limit = null) {
            IEnumerable<string> files = GetFiles(searchPattern, limit);
            using (var store = GetIsolatedStorage()) {
                return files.Select(path => new FileInfo {
                    Path = path,
                    Modified = store.GetLastWriteTime(path).LocalDateTime,
                    Created = store.GetCreationTime(path).LocalDateTime,
                    Size = GetFileSize(store, path)
                }).ToList();
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
            } catch {}
            return null;
        }
    }
}
