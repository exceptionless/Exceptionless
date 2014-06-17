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
        private readonly IsolatedStorageFileStorage _storage;

        public IsolatedStorageFileExceptionlessLog(string filePath, bool append = false) : base(filePath, append) {
            _storage = new IsolatedStorageFileStorage();
        }

        protected override StreamWriter GetWriter(bool append = false) {
            IsolatedStorageFileStream stream = _storage.NewWriteStream(FilePath, append);
            return new StreamWriter(stream);
        }

        protected override FileStream GetReader() {
            return _storage.NewReadStream(FilePath);
        }

        protected override long GetFileSize() {
            return _storage.GetFileSize(FilePath);
        }

        public override void Dispose() {
            base.Dispose();

            if (_storage != null)
                _storage.Dispose();
        }
    }
}