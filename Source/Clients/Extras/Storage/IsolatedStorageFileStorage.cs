using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Exceptionless.Storage;
using FileInfo = Exceptionless.Storage.FileInfo;

namespace Exceptionless.Extras.Storage {
    public class IsolatedStorageFileStorage : IFileStorage {
        private readonly IsolatedStorageFile _isolatedStorage;
        private readonly object _lockObject = new object();

        public IsolatedStorageFileStorage() {
            _isolatedStorage = IsolatedStorageFile.GetStore(IsolatedStorageScope.Assembly | IsolatedStorageScope.Machine, typeof(IsolatedStorageFileStorage), null);
        }

        public IsolatedStorageFile IsolatedStorage { get { return _isolatedStorage; } }

        public long GetFileSize(string path) {
            string fullPath = IsolatedStorage.GetFullPath(path);
            try {
                if (File.Exists(fullPath))
                    return new System.IO.FileInfo(fullPath).Length;
            } catch (IOException ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error getting size of file: {0}", ex.Message);
            }

            return -1;
        }

        public IEnumerable<string> GetFiles(string searchPattern = null) {
            var result = new List<string>();
            var stack = new Stack<string>();

            const string initialDirectory = "*";
            stack.Push(initialDirectory);

            while (stack.Count > 0) {
                string dir = stack.Pop();

                string directoryPath;
                if (dir == "*")
                    directoryPath = "*";
                else
                    directoryPath = dir + @"\*";

                var filesInCurrentDirectory = IsolatedStorage.GetFileNames(directoryPath).ToList();
                var filesInCurrentDirectoryWithFolderName = filesInCurrentDirectory.Select(file => Path.Combine(dir, file)).ToList();
                if (dir != "*")
                    result.AddRange(filesInCurrentDirectoryWithFolderName);
                else
                    result.AddRange(filesInCurrentDirectory);

                foreach (string directoryName in IsolatedStorage.GetDirectoryNames(directoryPath))
                    stack.Push(dir == "*" ? directoryName : Path.Combine(dir, directoryName));
            }

            if (String.IsNullOrEmpty(searchPattern))
                return result;

            var regex = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*?") + "$");
            return result.Where(k => regex.IsMatch(k));
        }

        public bool FileExists(string path) {
            return GetFiles(path).Any();
        }

        public string GetFileContents(string path) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            try {
                using (IsolatedStorageFileStream isoStream = NewReadStream(path)) {
                    using (var reader = new StreamReader(isoStream))
                        return reader.ReadToEnd();
                }
            } catch (Exception) {
                return null;
            }
        }

        public bool SaveFile(string path, string contents) {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException("path");

            EnsureDirectory(path);

            try {
                lock (_lockObject) {
                    using (IsolatedStorageFileStream isoStream = NewWriteStream(path))
                        using (var streamWriter = new StreamWriter(isoStream))
                            streamWriter.Write(contents);
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
                    IsolatedStorage.MoveFile(oldpath, newpath);
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
                    IsolatedStorage.DeleteFile(path);
                }
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public IEnumerable<FileInfo> GetFileList(string searchPattern = null) {
            IEnumerable<string> files = GetFiles(searchPattern);
            return files.Select(path => new FileInfo {
                Path = path,
                Modified = IsolatedStorage.GetLastWriteTime(path).LocalDateTime,
                Created = IsolatedStorage.GetCreationTime(path).LocalDateTime,
                Size = GetFileSize(path)
            });
        }

        public IsolatedStorageFileStream NewReadStream(string path) {
            return InternalCreateStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public IsolatedStorageFileStream NewWriteStream(string path, bool append = false) {
            FileMode mode = append ? FileMode.OpenOrCreate : FileMode.Create;
            return InternalCreateStream(path, mode, FileAccess.Write, FileShare.Read);
        }

        public IsolatedStorageFileStream NewReadWriteStream(string path, bool append = false) {
            FileMode mode = append ? FileMode.OpenOrCreate : FileMode.Create;
            return InternalCreateStream(path, mode, FileAccess.ReadWrite, FileShare.Read);
        }

        private IsolatedStorageFileStream InternalCreateStream(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int count = 0) {
            const int MAX_ISO_STORE_FILE_OPEN_TRIES = 3;
            IsolatedStorageFileStream stream;

            try {
                stream = new IsolatedStorageFileStream(path, fileMode, fileAccess, fileShare, IsolatedStorage);
            } catch (IsolatedStorageException) {
                Thread.Sleep(100);
                count++;
                if (count >= MAX_ISO_STORE_FILE_OPEN_TRIES)
                    throw;

                stream = InternalCreateStream(path, fileMode, fileAccess, fileShare, count);
            }

            return stream;
        }

        private readonly Collection<string> _ensuredDirectories = new Collection<string>(); 
        private void EnsureDirectory(string path) {
            string directory = Path.GetDirectoryName(path);
            if (String.IsNullOrEmpty(directory))
                return;

            if (_ensuredDirectories.Contains(directory))
                return;

            if (!IsolatedStorage.DirectoryExists(directory))
                IsolatedStorage.CreateDirectory(directory);
        }

        private bool _disposed;
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing) {
            if (_disposed)
                return;

            if (disposing) {
                IsolatedStorage.Close();
                IsolatedStorage.Dispose();
            }
            _disposed = true;
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
