#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Client.Tests;
using Exceptionless.Models;
using Exceptionless.Queue;
using Exceptionless.Utility;
using Xunit;

namespace Exceptionless.Tests.Queue {
    public class QueueStoreTests {
        private readonly IConfigurationAndLogAccessor _accessor = new TestConfigurationAndLogAccessor();

        [Fact]
        public void QueueManager() {
            Error error = CreateSampleError();
            var manager = new QueueManager(_accessor, new IsolatedStorageQueueStore("test", _accessor));

            manager.Enqueue(error);
            // verify that the extended data objects were serialized to JSON
            ValidateSampleError(error);

            IEnumerable<Manifest> manifests = manager.GetManifests();
            Assert.NotNull(manifests);
            Assert.Equal(1, manifests.Count());

            Error e = manager.GetError(manifests.First().Id);
            ValidateSampleError(e);

            manager.Delete(error.Id);
        }

        [Fact]
        public void IsolatedStorageQueueStore() {
            var store = new IsolatedStorageQueueStore("dir", _accessor);
            ValidateStore(store);
        }

        [Fact]
        public void MultipleQueueManager() {
            int manifestsProcessed = 0;
            var manager = new QueueManager(_accessor, new IsolatedStorageQueueStore("test", _accessor));
            List<Manifest> manifests = manager.GetManifests().ToList();
            foreach (Manifest manifest in manifests)
                manager.Delete(manifest.Id);

            const int NUMBER_TO_PROCESS = 20;
            for (int i = 0; i < NUMBER_TO_PROCESS; i++)
                manager.Enqueue(CreateSampleError());

            var mutex = new Mutex(false, "test");
            Parallel.For(0, 5, i => {
                mutex.WaitOne();
                var localManager = new QueueManager(_accessor, new IsolatedStorageQueueStore("test", _accessor));
                List<Manifest> localManifests = localManager.GetManifests().ToList();
                foreach (Manifest m in localManifests)
                    localManager.Delete(m.Id);
                Interlocked.Add(ref manifestsProcessed, localManifests.Count);
                Assert.NotNull(localManifests);
                mutex.ReleaseMutex();
            });

            Assert.Equal(NUMBER_TO_PROCESS, manifestsProcessed);

            foreach (Manifest manifest in manifests)
                manager.Delete(manifest.Id);
        }

        [Fact]
        public void FolderQueueStore() {
            var store = new FolderQueueStore("dir", _accessor);
            ValidateStore(store);
        }

        [Fact]
        public void InMemoryQueueStore() {
            var store = new InMemoryQueueStore();
            ValidateStore(store);
        }

        private void ValidateStore(IQueueStore store) {
            Assert.True(store.VerifyStoreIsUsable());

            // delete any old reports
            store.Cleanup(DateTime.Now);
            IEnumerable<Manifest> manifests = store.GetManifests(null);
            Assert.Equal(manifests.Count(), 0);

            Error error = CreateSampleError();
            Exceptionless.Queue.QueueManager.SerializeErrorExtendedData(_accessor, error);
            store.Enqueue(error);
            manifests = store.GetManifests(null);

            Assert.NotNull(manifests);
            Assert.Equal(manifests.Count(), 1);

            foreach (Manifest m in manifests) {
                m.Attempts++;
                m.LastAttempt = DateTime.Now;
                store.UpdateManifest(m);
            }

            manifests = store.GetManifests(null);

            Assert.NotNull(manifests);
            Assert.Equal(manifests.Count(), 1);

            foreach (Manifest m in manifests) {
                Assert.Equal(m.Attempts, 1);
                Assert.NotEqual(m.LastAttempt, DateTime.MinValue);

                Error e = store.GetError(m.Id);
                ValidateSampleError(e);

                store.Delete(m.Id);
            }

            manifests = store.GetManifests(null);

            Assert.NotNull(manifests);
            Assert.Equal(manifests.Count(), 0);
        }

        private Error CreateSampleError(bool includeContact = true) {
            var error = new Error {
                Id = ObjectId.GenerateNewId().ToString(),
                Message = MESSAGE,
                UserDescription = USER_DESCRIPTION
            };

            if (includeContact)
                error.UserEmail = USER_EMAIL;

            AddDefaultExtendedData(error);
            Assert.Equal(4, error.ExtendedData.Count);
            Assert.Equal(_anonymousType, error.ExtendedData[ANONYMOUS_TYPE_KEY]);
            Assert.Equal(AGE, error.ExtendedData[AGE_KEY]);
            Assert.Equal(MIN_DATETIME, error.ExtendedData[MIN_DATETIME_KEY]);
            Assert.Equal(VERSION, error.ExtendedData[VERSION_KEY]);

            return error;
        }

        private void AddDefaultExtendedData(Error error) {
            error.ExtendedData[ANONYMOUS_TYPE_KEY] = _anonymousType;
            error.ExtendedData[AGE_KEY] = AGE;
            error.ExtendedData[MIN_DATETIME_KEY] = MIN_DATETIME;
            error.ExtendedData[VERSION_KEY] = VERSION;
        }

        private void ValidateSampleError(Error error) {
            Assert.NotNull(error);
            Assert.Equal(MESSAGE, error.Message);
            Assert.NotNull(error.ExtendedData);
            Assert.Equal(4, error.ExtendedData.Count);
            Assert.Equal(ANONYMOUS_TYPE_JSON, error.ExtendedData[ANONYMOUS_TYPE_KEY]);
            Assert.Equal(AGE_JSON, error.ExtendedData[AGE_KEY]);
            Assert.Equal(MIN_DATETIME_JSON, error.ExtendedData[MIN_DATETIME_KEY]);
            Assert.Equal(VERSION, error.ExtendedData[VERSION_KEY]);
        }

        private const string MESSAGE = "This is a test from the client";
        private const string USER_DESCRIPTION = "This is the description from the client";
        private const string USER_EMAIL = "client@exceptionless.com";

        private const string VERSION_KEY = "Version";
        private const string VERSION = "1.0.0.0";
        private const string AGE_KEY = "Age";
        private const int AGE = 20;
        private const string AGE_JSON = "20";
        private const string MIN_DATETIME_KEY = "DateTime.MinValue";
        private readonly DateTime MIN_DATETIME = DateTime.MinValue;
        private const string MIN_DATETIME_JSON = "\"0001-01-01T00:00:00\"";
        private const string ANONYMOUS_TYPE_KEY = "AnonymousType";
        private const string ANONYMOUS_TYPE_JSON = "{\r\n  \"ContentType\": \"text/xml\",\r\n  \"Namespace\": \"uri://somenamespace\",\r\n  \"Data\": \"PHNvbWV4bWwvPg==\"\r\n}";

        private readonly object _anonymousType = new {
            ContentType = "text/xml",
            Namespace = "uri://somenamespace",
            Data = Encoding.UTF8.GetBytes("<somexml/>")
        };
    }
}