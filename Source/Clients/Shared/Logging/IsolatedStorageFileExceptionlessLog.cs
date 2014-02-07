#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.IO;
using System.IO.IsolatedStorage;
using Exceptionless.Utility;

namespace Exceptionless.Logging {
    public class IsolatedStorageFileExceptionlessLog : FileExceptionlessLog {
        private readonly IsolatedStorageDirectory _directory;

        public IsolatedStorageFileExceptionlessLog(string subDirectory, string filename, bool append = false) : base(filename, append) {
            _directory = new IsolatedStorageDirectory(subDirectory);
        }

        protected override StreamWriter GetWriter(bool append = false) {
            IsolatedStorageFileStream stream = _directory.NewWriteStream(FilePath, append);
            return new StreamWriter(stream);
        }

        protected override FileStream GetReader() {
            return _directory.NewReadStream(FilePath);
        }

        protected override long GetFileSize() {
            return _directory.GetFileSize(FilePath);
        }

        public override void Dispose() {
            base.Dispose();

            if (_directory != null)
                _directory.Dispose();
        }
    }
}