using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Configuration {
    public class ConfigurationTests : TestBase {
        public ConfigurationTests(ITestOutputHelper output) : base(output) {}

        [Fact]
        public void CanParseConnectionString() {
            const string connectionString = "provider=azurestorage;DefaultEndpointsProtocol=https;AccountName=test;AccountKey=nx4TKwaaaaaaaaaa8t51oPyOErc/4N0TOjrMy6aaaaaabDMbFiK+Gf5rLr6XnU1aaaaaqiX2Yik7tvLcwp4lw==;EndpointSuffix=core.windows.net";
            var data = connectionString.ParseConnectionString();
            Assert.Equal(5, data.Count);
            Assert.Equal("azurestorage", data.GetString("provider"));
            Assert.Equal("https", data.GetString("DefaultEndpointsProtocol"));
            Assert.Equal("test", data.GetString("AccountName"));
            Assert.Equal("nx4TKwaaaaaaaaaa8t51oPyOErc/4N0TOjrMy6aaaaaabDMbFiK+Gf5rLr6XnU1aaaaaqiX2Yik7tvLcwp4lw==", data.GetString("AccountKey"));
            Assert.Equal("core.windows.net", data.GetString("EndpointSuffix"));
            
            Assert.Equal(connectionString, data.BuildConnectionString());
        }
        
        [Fact]
        public void CanParseConnectionStringWithWhiteSpace() {
            const string connectionString = "provider = azurestorage; = ; DefaultEndpointsProtocol = https   ;";
            var data = connectionString.ParseConnectionString();
            Assert.Equal(2, data.Count);
            Assert.Equal("azurestorage", data.GetString("provider"));
            Assert.Equal("https", data.GetString("DefaultEndpointsProtocol"));
            
            Assert.Equal("provider=azurestorage;DefaultEndpointsProtocol=https", data.BuildConnectionString());
            Assert.Equal("DefaultEndpointsProtocol=https", data.BuildConnectionString(new HashSet<string> { "provider" }));
        }
        
        [Fact]
        public void CanParseConnectionStringWithNoKey() {
            const string connectionString = "http://localhost:9200";
            var data = connectionString.ParseConnectionString();
            Assert.Equal(1, data.Count);
            Assert.Equal(connectionString, data.GetString(String.Empty));
            
            Assert.Equal(connectionString, data.BuildConnectionString());
        }
    }
}
