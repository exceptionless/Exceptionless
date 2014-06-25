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
using Exceptionless.Extras.Utility;
using Exceptionless.Utility;

namespace Exceptionless.Logging {
    public class IsolatedStorageFileExceptionlessLog : FileExceptionlessLog {
        public IsolatedStorageFileExceptionlessLog(string filePath, bool append = false) : base(filePath, append) {}

        protected override void Init() {}

        private IsolatedStorageFile GetStore() {
            return Run.WithRetries(() => IsolatedStorageFile.GetStore(IsolatedStorageScope.Machine | IsolatedStorageScope.Assembly, typeof(IsolatedStorageFileStorage), null));
        }

        protected override WrappedDisposable<StreamWriter> GetWriter(bool append = false) {
            var store = GetStore();
            return new WrappedDisposable<StreamWriter>(new StreamWriter(new IsolatedStorageFileStream(FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, store)), store.Dispose);
        }

        protected override WrappedDisposable<FileStream> GetReader() {
            var store = GetStore();
            return new WrappedDisposable<FileStream>(new IsolatedStorageFileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, store), store.Dispose);
        }

        protected override long GetFileSize() {
            using (var store = GetStore()) {
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