#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Utility;
using Xunit;
using ClientConfiguration = Exceptionless.Configuration.ClientConfiguration;

namespace Exceptionless.Tests.Configuration {
    public class ClientConfigurationTests {
        private const string DEFAULT_STORE = "e3d51ea6";
        private const string CONFIG_FILENAME = "Server.config";

        [Fact]
        public void CanEnableLoggingFromConfig() {
            var client = new ExceptionlessClient();
            ClientConfiguration config = ClientConfiguration.Create(client);
            string logPath = config.LogPath;
            Assert.NotNull(config);

            Assert.True(config.EnableLogging);
            //Assert.Equal(logPath, config.LogPath); // TODO: Should this even be set to a value?? It's Isolated storage?
            Assert.NotNull(client.Log);
            Assert.NotNull(client);

            Assert.Equal(typeof(SafeExceptionlessLog), client.Log.GetType());

            client.ProcessQueue();
            client.Shutdown();

            var dir = new IsolatedStorageDirectory(DEFAULT_STORE);
            Assert.True(dir.FileExists(logPath));
            string content = dir.ReadFileAsString(logPath);
            Assert.True(content.Length > 10);
        }

        [Fact(Skip = "Need to set a logPath in the config to use this test.")]
        public void CanEnableLoggingFromConfigToFile() {
            const string logPath = "C:\\exceptionless.log";
            if (File.Exists(logPath))
                File.Delete(logPath);

            var client = new ExceptionlessClient();
            ClientConfiguration config = ClientConfiguration.Create(client);
            Assert.NotNull(config);

            Assert.True(config.EnableLogging);
            Assert.Equal(logPath, config.LogPath);
            Assert.NotNull(client.Log);
            Assert.NotNull(client.Log);

            Assert.Equal(typeof(FileExceptionlessLog), client.Log.GetType());

            client.ProcessQueue();
            client.Shutdown();

            Assert.True(File.Exists(logPath));
            string content = File.ReadAllText(logPath);
            Assert.True(content.Length > 10);
        }

        [Fact]
        public void CanReadConfiguration() {
            var client = new ExceptionlessClient();
            ClientConfiguration config = ClientConfiguration.Create(client);

            Assert.NotNull(config);

            Assert.True(config.ContainsKey("AttributeOnly"));
            Assert.Equal(config["AttributeOnly"], "Attribute");

            Assert.True(config.ContainsKey("UserNamespaces"));
            Assert.Equal(config["UserNamespaces"], "Exceptionless,FromConfig");

            Assert.True(config.ContainsKey("ConfigAndAttribute"));
            Assert.Equal(config["ConfigAndAttribute"], "Config");

            Assert.True(config.ContainsKey("AppConfigOnly"));
            Assert.Equal(config["AppConfigOnly"], "Config");
        }

        [Fact]
        public void CanReadCachedServerConfig() {
            var serverConfig = new SettingsDictionary {
                { "FromServer", "Server" }
            };
            var client = new ExceptionlessClient();
            ClientConfiguration.ProcessServerConfigResponse(client, serverConfig, DEFAULT_STORE);

            ClientConfiguration config = ClientConfiguration.Create(client);

            Assert.NotNull(config);

            Assert.True(config.ContainsKey("FromServer"));
            Assert.Equal(config["FromServer"], "Server");

            Assert.True(config.ContainsKey("AttributeOnly"));
            Assert.Equal(config["AttributeOnly"], "Attribute");

            Assert.True(config.ContainsKey("UserNamespaces"));
            Assert.Equal(config["UserNamespaces"], "Exceptionless,FromConfig");

            Assert.True(config.ContainsKey("ConfigAndAttribute"));
            Assert.Equal(config["ConfigAndAttribute"], "Config");

            Assert.True(config.ContainsKey("AppConfigOnly"));
            Assert.Equal(config["AppConfigOnly"], "Config");
        }

        [Fact]
        public void CanHandleInvalidCachedServerConfig() {
            using (var dir = new IsolatedStorageDirectory(DEFAULT_STORE)) {
                dir.WriteFile(CONFIG_FILENAME, "sadf<sdf>");

                Assert.True(dir.FileExists(CONFIG_FILENAME));

                var client = new ExceptionlessClient();
                ClientConfiguration config = ClientConfiguration.Create(client);

                // file should get deleted if it's invalid
                Assert.False(dir.FileExists(CONFIG_FILENAME));

                Assert.NotNull(config);

                Assert.True(config.ContainsKey("AttributeOnly"));
                Assert.Equal(config["AttributeOnly"], "Attribute");

                Assert.True(config.ContainsKey("UserNamespaces"));
                Assert.Equal(config["UserNamespaces"], "Exceptionless,FromConfig");

                Assert.True(config.ContainsKey("ConfigAndAttribute"));
                Assert.Equal(config["ConfigAndAttribute"], "Config");

                Assert.True(config.ContainsKey("AppConfigOnly"));
                Assert.Equal(config["AppConfigOnly"], "Config");
            }
        }
    }
}