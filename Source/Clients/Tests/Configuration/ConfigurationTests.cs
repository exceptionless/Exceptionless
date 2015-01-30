using System;
using System.Collections.Generic;
using Client.Tests.Utility;
using Exceptionless;
using Exceptionless.Configuration;
using Exceptionless.Core;
using Exceptionless.Dependency;
using Exceptionless.Models;
using Exceptionless.Storage;
using Exceptionless.Submission;
using Moq;
using Xunit;

[assembly: Exceptionless("LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw", ServerUrl = "http://localhost:45000", EnableSSL = false)]
[assembly: ExceptionlessSetting("testing", "configuration")]
namespace Client.Tests.Configuration {
    public class ConfigurationTests {
        [Fact]
        public void CanConfigureApiKeyFromClientConstructor() {
            var client = new ExceptionlessClient("LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw");
            Assert.NotNull(client);
            Assert.Equal("LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw", client.Configuration.ApiKey);
        }

        [Fact]
        public void CanConfigureClientUsingActionMethod() {
            const string version = "1.2.3";
            
            var client = new ExceptionlessClient(c => {
                c.ApiKey = "LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw";
                c.ServerUrl = Settings.Current.BaseURL;
                c.EnableSSL = false;
                c.SetVersion(version);
            });

            Assert.Equal("LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw", client.Configuration.ApiKey);
            Assert.Equal("http://localhost:45000", client.Configuration.ServerUrl);
            Assert.False(client.Configuration.EnableSSL);
            Assert.Equal(version, client.Configuration.DefaultData[Event.KnownDataKeys.Version].ToString());
            
        }

        [Fact]
        public void CanReadFromAttributes() {
            var config = new ExceptionlessConfiguration(DependencyResolver.CreateDefault());
            Assert.Null(config.ApiKey);
            Assert.Equal("https://collector.exceptionless.io", config.ServerUrl);
            Assert.True(config.EnableSSL);
            Assert.Equal(0, config.Settings.Count);

            config.ReadFromAttributes(typeof(ConfigurationTests).Assembly);
            Assert.Equal("LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw", config.ApiKey);
            Assert.Equal("http://localhost:45000", config.ServerUrl);
            Assert.False(config.EnableSSL);
            Assert.Equal(1, config.Settings.Count);
            Assert.Equal("configuration", config.Settings["testing"]);
        }

        [Fact]
        public void WillLockConfig() {
            var client = new ExceptionlessClient();
            client.Configuration.Resolver.Register<ISubmissionClient, InMemorySubmissionClient>();
            client.Configuration.ApiKey = "LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw";
            client.SubmitEvent(new Event());
            Assert.Throws<ArgumentException>(() => client.Configuration.ApiKey = "blah");
            Assert.Throws<ArgumentException>(() => client.Configuration.ServerUrl = "blah");
        }

        [Fact]
        public void CanUpdateSettingsFromServer() {
            var config = new ExceptionlessConfiguration(DependencyResolver.Default);
            config.ApiKey = "LhhP1C9gijpSKCslHHCvwdSIz298twx271n1l6xw";
            config.Settings["LocalSetting"] = "1";
            config.Settings["LocalSettingToOverride"] = "1";

            var submissionClient = new Mock<ISubmissionClient>();
            submissionClient.Setup(m => m.PostEvents(It.IsAny<IEnumerable<Event>>(), config, It.IsAny<IJsonSerializer>()))
                .Callback(() => SettingsManager.CheckVersion(1, config))
                .Returns(() => new SubmissionResponse(202, "Accepted"));
            submissionClient.Setup(m => m.GetSettings(config, It.IsAny<IJsonSerializer>()))
                .Returns(() => new SettingsResponse(true, new SettingsDictionary { { "Test", "Test" }, { "LocalSettingToOverride", "2" } }, 1));

            config.Resolver.Register<ISubmissionClient>(submissionClient.Object);
            var client = new ExceptionlessClient(config);

            Assert.Equal(2, client.Configuration.Settings.Count);
            Assert.False(client.Configuration.Settings.ContainsKey("Test"));
            Assert.Equal("1", client.Configuration.Settings["LocalSettingToOverride"]);
            client.SubmitEvent(new Event { Type = "Log", Message = "Test" });
            client.ProcessQueue();
            Assert.True(client.Configuration.Settings.ContainsKey("Test"));
            Assert.Equal("2", client.Configuration.Settings["LocalSettingToOverride"]);
            Assert.Equal(3, client.Configuration.Settings.Count);

            var storage = config.Resolver.GetFileStorage() as InMemoryFileStorage;
            Assert.True(storage.Exists(config.GetQueueName() + "\\server-settings.json"));

            config.Settings.Clear();
            config.ApplySavedServerSettings();
            Assert.True(client.Configuration.Settings.ContainsKey("Test"));
            Assert.Equal("2", client.Configuration.Settings["LocalSettingToOverride"]);
            Assert.Equal(2, client.Configuration.Settings.Count);
        }
    }
}
