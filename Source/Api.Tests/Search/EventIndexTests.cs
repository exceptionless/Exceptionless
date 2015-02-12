using System;
using System.Collections.Generic;
using System.IO;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
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
        [InlineData("1ecd0826e447ad1e78877555", 2)]
        public void GetByOrganizationId(string id, int count) {
            var result = GetByFilter("organization:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447ad1e78877ab2", 2)]
        public void GetByProjectId(string id, int count) {
            var result = GetByFilter("project:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData("000000000000000000000000", 0)]
        [InlineData("1ecd0826e447a44e78877ab1", 1)]
        [InlineData("2ecd0826e447a44e78877ab2", 1)]
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
        [InlineData("null", 0)]
        [InlineData("00000", 0)]
        [InlineData("12345", 1)]
        public void GetBySessionId(string id, int count) {
            var result = GetByFilter("session:" + id);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData("null", 0)]
        [InlineData("log", 1)]
        [InlineData("error", 1)]
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
        // TODO: Add some range queries.
        [InlineData("\"2014-12-09T17:28:44.966\"", 1)]
        [InlineData("\"2014-12-09T17:28:44.966+00:00\"", 1)]
        [InlineData("\"2015-02-11T20:54:04.3457274+00:00\"", 1)]
        public void GetByDate(string date, int count) {
            var result = GetByFilter("date:" + date);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }

        [Theory]
        [InlineData(false, 1)]
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
        [InlineData(1, 1)]
        public void GetByValue(int value, int count) {
            var result = GetByFilter("value:" + value);
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 1)]
        public void GetByFixed(bool @fixed, int count) {
            var result = GetByFilter("fixed:" + @fixed.ToString().ToLower());
            Assert.NotNull(result);
            Assert.Equal(count, result.Count);
        }
        
        [Theory]
        [InlineData(false, 1)]
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
        [InlineData("Mozilla", 1)]
        [InlineData("\"Mozilla/5.0\"", 1)]
        [InlineData("5.0", 1)]
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

        // TODO: Add tests for non existent.
        //http://www.elasticsearch.org/guide/en/elasticsearch/reference/current/query-dsl-query-string-query.html#query-string-syntax

//browser	browser:Chrome	true	Events	Browser
//browser.version	browser.version:50.0	true	Events	Browser version
//browser.major	browser.major:50	true	Events	Browser major version
//device	device:iPhone	true	Events	Device
//os	os:"iOS 8"	true	Events	Operating System
//os.version	os.version:8.0	true	Events	Operating System version
//os.major	os.major:8	true	Events	Operating System major version
        
        [Theory]
        //[InlineData(false, 1)]// -bot:true != bot:false
        [InlineData(true, 1)]
        public void GetByBot(bool bot, int count) {
            var result = GetByFilter("bot:" + bot.ToString().ToLower());
            Assert.NotNull(result); 
            Assert.Equal(count, result.Count);
        }

//error.code	error.code:500 or 500	false	Events	Error code
//error.message	error.message:"A NullReferenceException occurred" or "A NullReferenceException occurred"	false	Events	Error message
//error.type	error.type:"System.NullReferenceException" or "System.NullReferenceException"	false	Events	Error type
//user	user:"random user identifier" or "random user identifier"	false	Events	Identity assigned to the event
//description	description:"My description" or "My Description"	false	Stacks	Description
//user.description	user.description:"I clicked the button" or "I clicked the button"	false	Events	User Description
//user.email	user.email:"support@exceptionless.io" or "support@exceptionless.io"	false	Events	User Email Address

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