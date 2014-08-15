#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Extras.Storage;
using Exceptionless.Logging;

namespace Exceptionless.Client.Tests.Log {
    public class IsolatedStorageFileExceptionlessLogTests : FileExceptionlessLogTests {
        private readonly IsolatedStorageFileStorage _storage;

        public IsolatedStorageFileExceptionlessLogTests() {
            _storage = new IsolatedStorageFileStorage();
        }

        protected override FileExceptionlessLog GetLog(string filePath) {
            return new IsolatedStorageFileExceptionlessLog(filePath);
        }

        protected override bool LogExists(string path = LOG_FILE) {
            return _storage.Exists(path);
        }

        protected override void DeleteLog(string path = LOG_FILE) {
            if (LogExists(path))
                _storage.DeleteFile(path);
        }

        protected override string GetLogContent(string path = LOG_FILE) {
            if (!LogExists(path))
                return String.Empty;

            return _storage.GetFileContents(path);
        }

        protected override long GetLogSize(string path = LOG_FILE) {
            var info = _storage.GetFileInfo(path);
            if (info != null)
                return info.Size;

            return -1;
        }

        public override void Dispose() {
            base.Dispose();

            if (_storage != null)
                _storage.Dispose();
        }
    }
}