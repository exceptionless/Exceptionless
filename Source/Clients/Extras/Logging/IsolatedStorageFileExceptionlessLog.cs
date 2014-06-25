#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.IO;
using System.IO.IsolatedStorage;
using Exceptionless.Extras.Storage;

namespace Exceptionless.Logging {
    public class IsolatedStorageFileExceptionlessLog : FileExceptionlessLog {
        public IsolatedStorageFileExceptionlessLog(string filePath, bool append = false) : base(filePath, append) {}

        private IsolatedStorageFile GetIsolatedStorage() {
            return IsolatedStorageFile.GetStore(IsolatedStorageScope.Machine | IsolatedStorageScope.Assembly, typeof(IsolatedStorageFileStorage), null);
        }

        protected override StreamWriter GetWriter(bool append = false) {
            using (var store = GetIsolatedStorage())
            using (var stream = new IsolatedStorageFileStream(FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, store))
                return new StreamWriter(stream);
        }

        protected override FileStream GetReader() {
            using (var store = GetIsolatedStorage())
            using (var stream = new IsolatedStorageFileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, store))
                return stream;
        }

        public long GetFileSize(string path) {
            using (var store = GetIsolatedStorage()) {
                string fullPath = store.GetFullPath(FilePath);
                try {
                    if (File.Exists(fullPath))
                        return new FileInfo(fullPath).Length;
                } catch (IOException ex) {
                    System.Diagnostics.Trace.WriteLine("Exceptionless: Error getting size of file: {0}", ex.Message);
                }

                return -1;
            }
        }
    }
}