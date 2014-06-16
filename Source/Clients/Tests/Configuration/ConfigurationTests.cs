using System;
using Exceptionless;
using Exceptionless.Configuration;
using Exceptionless.Core;
using Exceptionless.Dependency;
using Xunit;

[assembly: Exceptionless("e3d51ea621464280bbcb79c11fd6483e", ServerUrl = "http://localhost:45000", EnableSSL = false)]
[assembly: ExceptionlessSetting("testing", "configuration")]
namespace Client.Tests.Configuration {
    public class ConfigurationTests {
        [Fact]
        public void CanConfigureClientUsingActionMethod() {
            var client = new ExceptionlessClient(c => {
                c.ApiKey = "e3d51ea621464280bbcb79c11fd6483e";
                c.ServerUrl = Settings.Current.BaseURL;
                c.EnableSSL = false;
            });

            Assert.Equal("e3d51ea621464280bbcb79c11fd6483e", client.Configuration.ApiKey);
            Assert.Equal("http://localhost:45000", client.Configuration.ServerUrl);
            Assert.False(client.Configuration.EnableSSL);
        }

        [Fact]
        public void CanReadFromAttributes() {
            var config = new ExceptionlessConfiguration(DependencyResolver.CreateDefault());
            Assert.Null(config.ApiKey);
            Assert.Equal("https://collector.exceptionless.com", config.ServerUrl);
            Assert.True(config.EnableSSL);
            Assert.Equal(0, config.Settings.Count);

            config.ReadFromAttributes(typeof(ConfigurationTests).Assembly);
            Assert.Equal("e3d51ea621464280bbcb79c11fd6483e", config.ApiKey);
            Assert.Equal("http://localhost:45000", config.ServerUrl);
            Assert.False(config.EnableSSL);
            Assert.Equal(1, config.Settings.Count);
            Assert.Equal("configuration", config.Settings["testing"]);
        }
    }
}
