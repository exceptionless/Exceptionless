#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using Exceptionless.Configuration;
using Exceptionless.Logging;
using Exceptionless.Utility;
using Xunit;

namespace Exceptionless.Tests.Configuration {
    public class LocalStorageTests : IDisposable {
        private const string DEFAULT_STORE = "test";
        private const string CONFIG_FILENAME = "Local.config";

        [Fact]
        public void CreateNewConfiguration() {
            DeleteConfig();

            var client = new ExceptionlessClient();
            LocalConfigurationDictionary localConfiguration = LocalConfigurationDictionary.Create(DEFAULT_STORE, client);
            Assert.NotNull(localConfiguration);
            Assert.False(localConfiguration.IsDirty);
            Assert.NotEqual(Guid.Empty, localConfiguration.InstallIdentifier);

            using (var store = new IsolatedStorageDirectory(DEFAULT_STORE)) {
                Assert.True(store.FileExists(CONFIG_FILENAME));
                Console.WriteLine(store.ReadFileAsString(CONFIG_FILENAME));
            }
        }

        [Fact]
        public void ReadExistingConfiguration() {
            Assert.False(String.IsNullOrEmpty(DEFAULT_STORE));
            var logAccessor = new NullExceptionlessLogAccessor();

            LocalConfigurationDictionary localConfiguration = LocalConfigurationDictionary.Create(DEFAULT_STORE, logAccessor);
            localConfiguration.EmailAddress = "client@exceptionless.com";
            localConfiguration.Save();

            localConfiguration = LocalConfigurationDictionary.Create(DEFAULT_STORE, logAccessor);
            Assert.NotNull(localConfiguration);
            Assert.False(localConfiguration.IsDirty);
            Assert.NotEqual(Guid.Empty, localConfiguration.InstallIdentifier);
            Assert.Equal("client@exceptionless.com", localConfiguration.EmailAddress);
        }

        [Fact]
        public void IsDirtyTests() {
            DeleteConfig();

            var client = new ExceptionlessClient();
            LocalConfigurationDictionary localConfiguration = LocalConfigurationDictionary.Create(DEFAULT_STORE, client);
            Assert.NotNull(localConfiguration);

            Assert.False(localConfiguration.IsDirty);
            localConfiguration.StartCount++;

            Assert.True(localConfiguration.IsDirty); // TODO: this fails because of line 63 in ObservableConcurrentDictionary. It fails to fire the event in real time.

            Assert.True(localConfiguration.Save());
            Assert.False(localConfiguration.IsDirty);
        }

        [Fact]
        public void MultiThreadedSaveLocalConfiguration() {
            DeleteConfig();
            var client = new ExceptionlessClient();

            Parallel.For(0, 200, i => {
                Exception exception = Record.Exception(() => {
                    LocalConfigurationDictionary localConfiguration = LocalConfigurationDictionary.Create(DEFAULT_STORE, client);
                    Assert.NotNull(localConfiguration);

                    localConfiguration.IsDirty = true;
                    localConfiguration["ExpireTokenDate"] = DateTime.Now.AddMinutes(i).ToString();
                    localConfiguration.StartCount++;
                    Task.Factory.StartNew(() => localConfiguration["ExpireTokenDate"] = DateTime.Now.AddMinutes(i + 1).ToString());
                    localConfiguration.Save();

                    Assert.NotNull(LocalConfigurationDictionary.Create(DEFAULT_STORE, client));

                    localConfiguration.IsDirty = true;
                    localConfiguration["ExpireTokenDate"] = DateTime.Now.AddMinutes(i).ToString();
                    Task.Factory.StartNew(() => localConfiguration.Remove("ExpireTokenDate"));
                    localConfiguration.Save();

                    Assert.NotNull(LocalConfigurationDictionary.Create(DEFAULT_STORE, client));

                    localConfiguration.StartCount++;
                    localConfiguration.IsDirty = true;
                    Assert.True(localConfiguration.Save(), "Saved");

                    Assert.NotNull(LocalConfigurationDictionary.Create(DEFAULT_STORE, client));
                });

                Assert.Null(exception);
            });

            using (var store = new IsolatedStorageDirectory(DEFAULT_STORE)) {
                Console.WriteLine(store.GetFullPath(CONFIG_FILENAME));
                Assert.True(store.FileExists(CONFIG_FILENAME));
                Console.WriteLine(store.ReadFileAsString(CONFIG_FILENAME));
            }
        }

        [Fact]
        public void ReadCorruptedConfiguration() {
            Assert.False(String.IsNullOrEmpty(DEFAULT_STORE));
            var client = new ExceptionlessClient();

            using (var dir = new IsolatedStorageDirectory(DEFAULT_STORE))
                dir.WriteFile(CONFIG_FILENAME, "<blah/>>>");

            Exception exception = Record.Exception(() => {
                client.IsConfigurationUpdateNeeded();

                LocalConfigurationDictionary localConfiguration = LocalConfigurationDictionary.Create(DEFAULT_STORE, client);
                Assert.NotNull(localConfiguration);
            });

            Assert.Null(exception);
        }

        private void DeleteConfig(string storeId = DEFAULT_STORE) {
            using (var dir = new IsolatedStorageDirectory(storeId)) {
                if (dir.FileExists(CONFIG_FILENAME))
                    dir.DeleteFile(CONFIG_FILENAME);
            }
        }

        public void Dispose() {
            DeleteConfig();
        }
    }
}