#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using Exceptionless.Core.Helpers;
using Exceptionless.Dependency;
using Exceptionless.Extras.Storage;
using Exceptionless.Logging;
using Exceptionless.Serializer;

namespace Exceptionless.Client.Tests.Log {
    public class IsolatedStorageFileExceptionlessLogTests : FileExceptionlessLogTests {
        private readonly IsolatedStorageObjectStorage _storage;

        public IsolatedStorageFileExceptionlessLogTests() {
            var resolver = new DefaultDependencyResolver();
            resolver.Register<IExceptionlessLog, NullExceptionlessLog>();
            resolver.Register<IJsonSerializer, DefaultJsonSerializer>();
            _storage = new IsolatedStorageObjectStorage(resolver);
        }

        protected override FileExceptionlessLog GetLog(string filePath) {
            return new IsolatedStorageFileExceptionlessLog(filePath);
        }

        protected override bool LogExists(string path = LOG_FILE) {
            return _storage.Exists(path);
        }

        protected override void DeleteLog(string path = LOG_FILE) {
            if (LogExists(path))
                _storage.DeleteObject(path);
        }

        public override void Dispose() {
            base.Dispose();

            if (_storage != null)
                _storage.Dispose();
        }
    }
}