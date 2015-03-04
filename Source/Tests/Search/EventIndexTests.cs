using System;
using System.Collections.Generic;
using System.IO;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories;
using Nest;
using Xunit;
using Xunit.Extensions;
using SortOrder = Exceptionless.Core.Repositories.SortOrder;

namespace Exceptionless.Api.Tests.Repositories {
    public class EventIndexTests {
        private readonly IEventRepository _repository = IoC.GetInstance<IEventRepository>();
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private static bool _createdEvents;

        public EventIndexTests() {
            if (!_createdEvents) {
                _createdEvents = true;
                CreateEvents();
            }
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("54dbc16ca0f5c61398427b00", 1)]
        [InlineData("54dbc16ca0f5c61398427b01", 1)]
        public void GetById(string id, int count) {
            var result = GetByFilter("id:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877555", 3)]
        public void GetByOrganizationId(string id, int count) {
            var result = GetByFilter("organization:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877ab2", 3)]
        public void GetByProjectId(string id, int count) {
            var result = GetByFilter("project:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 2)]
        public void GetByStackId(string id, int count) {
            var result = GetByFilter("stack:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("000000000", 0)]
        [InlineData("876554321", 1)]
        public void GetByReferenceId(string id, int count) {
            var result = GetByFilter("reference:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("00000", 0)]
        [InlineData("123452366", 1)]
        public void GetBySessionId(string id, int count) {
            var result = GetByFilter("session:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("log", 1)]
        [InlineData("error", 2)]
        [InlineData("custom", 0)]
        public void GetByType(string type, int count) {
            var result = GetByFilter("type:" + type);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("\"GET /Print\"", 1)]
        public void GetBySource(string source, int count) {
            var result = GetByFilter("source:" + source);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("Error", 1)]
        public void GetByLevel(string level, int count) {
            var result = GetByFilter("level:" + level);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("\"2014-12-09T17:28:44.966\"", 1)]
        [InlineData("\"2014-12-09T17:28:44.966+00:00\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274+00:00\"", 1)]
        public void GetByDate(string date, int count) {
            var result = GetByFilter("date:" + date);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public void GetByFirst(bool first, int count) {
            var result = GetByFilter("first:" + first.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("\"Invalid hash. Parameter name: hash\"", 1)] //see what the actual def is for the standard anaylizer
        [InlineData("message:\"Invalid hash. Parameter name: hash\"", 1)]
        public void GetByMessage(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("_missing_:tag", 1)]
        [InlineData("tag:test", 1)]
        [InlineData("tag:Blake", 1)]
        [InlineData("tag:Niemyjski", 1)]
        [InlineData("tag:\"Blake Niemyjski\"", 1)]
        public void GetByTag(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("_missing_:value", 2)]
        [InlineData("_exists_:value", 1)]
        [InlineData("value:1", 1)]
        [InlineData("value:>0", 1)]
        [InlineData("value:(>0 AND <=10)", 1)]
        public void GetByValue(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public void GetByFixed(bool @fixed, int count) {
            var result = GetByFilter("fixed:" + @fixed.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        public void GetByHidden(bool hidden, int count) {
            var result = GetByFilter("hidden:" + hidden.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("1.2", 1)]
        [InlineData("1.2.3", 1)]
        [InlineData("1.2.3.0", 1)]
        public void GetByVersion(string version, int count) {
            var result = GetByFilter("version:" + version);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("SERVER-01", 1)]
        [InlineData("machine:SERVER-01", 1)]
        [InlineData("machine:\"SERVER-01\"", 1)]
        public void GetByMachine(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("192.168.0.88", 1)]
        [InlineData("ip:192.168.0.88", 1)]
        [InlineData("10.0.0.208", 1)]
        [InlineData("ip:10.0.0.208", 1)]
        public void GetByIP(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("x86", 0)]
        [InlineData("x64", 1)]
        public void GetByArchitecture(string architecture, int count) {
            var result = GetByFilter("architecture:" + architecture);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("Mozilla", 2)]
        [InlineData("\"Mozilla/5.0\"", 2)]
        [InlineData("5.0", 2)]
        [InlineData("Macintosh", 1)]
        public void GetByUserAgent(string userAgent, int count) {
            var result = GetByFilter("useragent:" + userAgent);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("\"/user.aspx\"", 1)]
        [InlineData("path:\"/user.aspx\"", 1)]
        public void GetByPath(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("browser:Chrome", 2)]
        [InlineData("browser:\"Chrome Mobile\"", 1)]
        [InlineData("browser.raw:\"Chrome Mobile\"", 1)]
        public void GetByBrowser(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("browser.version:39.0.2171", 1)]
        [InlineData("browser.version:26.0.1410", 1)]
        [InlineData("browser.version.raw:26.0.1410", 1)]
        public void GetByBrowserVersion(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("39", 1)]
        [InlineData("26", 1)]
        public void GetByBrowserMajorVersion(string browserMajorVersion, int count) {
            var result = GetByFilter("browser.major:" + browserMajorVersion);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("device:Huawei", 1)]
        [InlineData("device:\"Huawei U8686\"", 1)]
        [InlineData("device.raw:\"Huawei U8686\"", 1)]
        public void GetByDevice(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("os:Android", 1)]
        [InlineData("os:Mac", 1)]
        [InlineData("os:\"Mac OS X\"", 1)]
        [InlineData("os.raw:\"Mac OS X\"", 1)]
        [InlineData("os:\"Microsoft Windows Server\"", 1)]
        [InlineData("os:\"Microsoft Windows Server 2012 R2 Standard\"", 1)]
        public void GetByOS(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("os.version:4.1.1", 1)]
        [InlineData("os.version:10.10.1", 1)]
        [InlineData("os.version.raw:10.10", 0)]
        [InlineData("os.version.raw:10.10.1", 1)]
        public void GetByOSVersion(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("4", 1)]
        [InlineData("10", 1)]
        public void GetByOSMajorVersion(string osMajorVersion, int count) {
            var result = GetByFilter("os.major:" + osMajorVersion);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("bot:false", 1)]
        [InlineData("-bot:true", 2)]
        [InlineData("bot:true", 1)]
        public void GetByBot(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result); 
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("500", 1)]
        [InlineData("error.code:\"-1\"", 1)]
        [InlineData("error.code:500", 1)]
        [InlineData("error.code:5000", 0)]
        public void GetByErrorCode(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("\"Invalid hash. Parameter name: hash\"", 1)]
        [InlineData("error.message:\"Invalid hash. Parameter name: hash\"", 1)]
        [InlineData("error.message:\"A Task's exception(s)\"", 1)]
        public void GetByErrorMessage(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("AssociateWithCurrentThread", 1)]
        [InlineData("error.targetmethod:AssociateWithCurrentThread", 1)]
        public void GetByErrorTargetMethod(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("Exception", 2)]
        [InlineData("error.targettype:Exception", 1)]
        [InlineData("error.targettype.raw:System.Exception", 1)]
        public void GetByErrorTargetType(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("NullReferenceException", 1)]
        [InlineData("System.NullReferenceException", 1)]
        [InlineData("error.type:NullReferenceException", 1)]
        [InlineData("error.type:System.NullReferenceException", 1)]
        [InlineData("error.type:System.Exception", 1)]
        public void GetByErrorType(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("My-User-Identity", 1)]
        [InlineData("user:My-User-Identity", 1)]
        [InlineData("example@exceptionless.com", 1)]
        [InlineData("user:example@exceptionless.com", 1)]
        [InlineData("user:exceptionless.com", 1)]
        [InlineData("example", 1)]
        public void GetByUser(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("Blake", 2)] // Matches due to user name and partial tag
        [InlineData("user.name:Blake", 1)]
        public void GetByUserName(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("test@exceptionless.com", 1)]
        [InlineData("user.email:test@exceptionless.com", 1)]
        [InlineData("user.email:exceptionless.com", 1)]
        public void GetByUserEmailAddress(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("\"my custom description\"", 1)]
        [InlineData("user.description:\"my custom description\"", 1)]
        public void GetByUserDescription(string filter, int count) {
            var result = GetByFilter(filter);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        //[Theory]
        //[InlineData("\"data.load time-s\":\"262", 1)]
        //public void GetByCustomData(string filter, int count) {
        //    var result = GetByFilter(filter);
        //    Assert.NotNull(result);
        //    Assert.Equal(count, result.Count);
        //}

        private void CreateEvents() {
            ElasticSearchConfiguration.ConfigureMapping(_client, true);

            var parserPluginManager = IoC.GetInstance<EventParserPluginManager>();
            foreach (var file in Directory.GetFiles(@"..\..\Search\Data\", "event*.json", SearchOption.AllDirectories)) {
                var events = parserPluginManager.ParseEvents(File.ReadAllText(file), 2, "exceptionless/2.0.0.0");
                Assert.NotNull(events);
                Assert.True(events.Count > 0);
                _repository.Add(events);
            }

            _client.Refresh();
        }

        private ICollection<PersistentEvent> GetByFilter(string filter) {
            return _repository.GetByFilter(null, filter, null, SortOrder.Descending, null, DateTime.MinValue, DateTime.MaxValue, new PagingOptions());
        }
    }
}