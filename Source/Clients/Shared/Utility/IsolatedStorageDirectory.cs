#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

#if !PORTABLE40
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection;
using System.Threading;
using Exceptionless.Serialization;

#if PFX_LEGACY_3_5
using System.Reflection;
#endif

namespace Exceptionless.Utility {
    internal class IsolatedStorageDirectory : IDisposable {
        private readonly IsolatedStorageFile _isolatedStorage;
        private readonly string _subDirectory;

        public IsolatedStorageDirectory(string subDirectory = null) {
            _isolatedStorage = IsolatedStorageFile.GetStore(IsolatedStorageScope.Assembly | IsolatedStorageScope.Machine, typeof(IsolatedStorageDirectory), null);
            _subDirectory = subDirectory;
            VerifySubDirectory();
        }

        public string SubDirectory { get { return _subDirectory; } }

        public IsolatedStorageFile IsolatedStorage { get { return _isolatedStorage; } }

        private void VerifySubDirectory() {
            if (IsolatedStorage.GetDirectoryNames(SubDirectory).Length == 0)
                IsolatedStorage.CreateDirectory(SubDirectory);
        }

        public void Dispose() {
#if !SILVERLIGHT
            IsolatedStorage.Close();
#endif
            IsolatedStorage.Dispose();
        }

        public string GetFullPath(string filename) {
            string path = Path.Combine(SubDirectory, filename);
            return IsolatedStorage.GetFullPath(path);
        }

        public long GetFileSize(string filename) {
            string path = Path.Combine(SubDirectory, filename);
            string fullPath = IsolatedStorage.GetFullPath(path);
            try
            {
                if (File.Exists(fullPath))
                    return new FileInfo(fullPath).Length;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error getting size of file: {0}", ex.Message);
            }

            return -1;
        }

        public IEnumerable<string> GetFiles(string searchPattern = null) {
            string searchPath = Path.Combine(SubDirectory, searchPattern ?? "*");
            return IsolatedStorage.GetFileNames(searchPath).ToList();
        }

        public IEnumerable<IsolatedStorageFileInfo> GetFilesWithTimes(string searchPattern = null) {
            IEnumerable<string> files = GetFiles(searchPattern);
            return files.Select(file => new IsolatedStorageFileInfo {
                FileName = file,
                LastWriteTime = IsolatedStorage.GetLastWriteTime(Path.Combine(SubDirectory, file)),
                CreationTime = IsolatedStorage.GetCreationTime(Path.Combine(SubDirectory, file))
            });
        }

        public bool FileExists(string filename) {
            return GetFiles(filename).Any();
        }

        public T ReadFile<T>(string filename) {
            using (IsolatedStorageFileStream isoStream = NewReadStream(filename)) {
                using (var reader = new StreamReader(isoStream))
                    return ModelSerializer.Current.Deserialize<T>(reader);
            }
        }

        public string ReadFileAsString(string filename) {
            using (IsolatedStorageFileStream isoStream = NewReadStream(filename)) {
                using (var reader = new StreamReader(isoStream))
                    return reader.ReadToEnd();
            }
        }

        public void WriteFile(string filename, object data) {
            using (IsolatedStorageFileStream isoStream = NewWriteStream(filename)) {
                using (var streamWriter = new StreamWriter(isoStream)) {
                    if (data is string)
                        streamWriter.Write(data);
                    else
                        ModelSerializer.Current.Serialize(streamWriter, data);
                }
            }
        }

        public bool DeleteFile(string filename) {
            string path = Path.Combine(SubDirectory, filename);
            if (IsolatedStorage.FileExists(path)) {
                IsolatedStorage.DeleteFile(path);
                return true;
            }

            return false;
        }

        public IsolatedStorageFileStream NewReadStream(string filename) {
            string path = Path.Combine(SubDirectory, filename);
            return InternalCreateStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public IsolatedStorageFileStream NewWriteStream(string filename, bool append = false) {
            string path = Path.Combine(SubDirectory, filename);
            FileMode mode = append ? FileMode.OpenOrCreate : FileMode.Create;
            return InternalCreateStream(path, mode, FileAccess.Write, FileShare.Read);
        }

        public IsolatedStorageFileStream NewReadWriteStream(string filename, bool append = false) {
            string path = Path.Combine(SubDirectory, filename);
            FileMode mode = append ? FileMode.OpenOrCreate : FileMode.Create;
            return InternalCreateStream(path, mode, FileAccess.ReadWrite, FileShare.Read);
        }

        private IsolatedStorageFileStream InternalCreateStream(string filename, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int count = 0) {
            const int MAX_ISO_STORE_FILE_OPEN_TRIES = 3;
            IsolatedStorageFileStream stream;

            try {
                stream = new IsolatedStorageFileStream(filename, fileMode, fileAccess, fileShare, IsolatedStorage);
            } catch (IsolatedStorageException) {
                Thread.Sleep(100);
                count++;
                if (count >= MAX_ISO_STORE_FILE_OPEN_TRIES)
                    throw;

                stream = InternalCreateStream(filename, fileMode, fileAccess, fileShare, count);
            }

            return stream;
        }
    }

    public class IsolatedStorageFileInfo {
        public string FileName { get; set; }
        public DateTimeOffset CreationTime { get; set; }
        public DateTimeOffset LastWriteTime { get; set; }
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

#if PFX_LEGACY_3_5
        public static bool FileExists(this IsolatedStorageFile storage, string path) {
            string[] files = storage.GetFileNames(path);
            return files.Length > 0;
        }
        
        public static DateTimeOffset GetCreationTime(this IsolatedStorageFile storage, string path) {
            try {
                var fullPath = storage.GetFullPath(path);
                return new DateTimeOffset(File.GetCreationTimeUtc(fullPath)).ToLocalTime();
            } catch (Exception) {
                return new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero).ToLocalTime();
            }
        }
        
        public static DateTimeOffset GetLastWriteTime(this IsolatedStorageFile storage, string path) {
            try {
                var fullPath = storage.GetFullPath(path);
                return new DateTimeOffset(File.GetLastAccessTimeUtc(fullPath)).ToLocalTime();
            } catch (Exception) {
                return new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero).ToLocalTime();
            }
        }
#endif
    }
}

#endif