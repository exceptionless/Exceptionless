#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Logging;
using Exceptionless.Utility;

namespace Exceptionless.Client.Tests.Log {
    public class IsolatedStorageFileExceptionlessLogTests : FileExceptionlessLogTests {
        private readonly IsolatedStorageDirectory _directory;

        public IsolatedStorageFileExceptionlessLogTests() {
            _directory = new IsolatedStorageDirectory("test");
        }

        protected override FileExceptionlessLog GetLog(string filePath) {
            return new IsolatedStorageFileExceptionlessLog("test", filePath);
        }

        protected override bool LogExists(string path = LOG_FILE) {
            return _directory.FileExists(path);
        }

        protected override void DeleteLog(string path = LOG_FILE) {
            if (LogExists(path))
                _directory.DeleteFile(path);
        }

        protected override string GetLogContent(string path = LOG_FILE) {
            if (!LogExists(path))
                return String.Empty;

            return _directory.ReadFileAsString(path);
        }

        protected override long GetLogSize(string path = LOG_FILE) {
            return _directory.GetFileSize(path);
        }

        public override void Dispose() {
            base.Dispose();

            if (_directory != null)
                _directory.Dispose();
        }
    }
}