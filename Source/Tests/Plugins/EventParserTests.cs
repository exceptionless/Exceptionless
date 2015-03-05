using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Newtonsoft.Json;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(HappyDiffReporter))]
    public class EventParserTests {
        private readonly EventParserPluginManager _eventParserPluginManager = IoC.GetInstance<EventParserPluginManager>();

        public static IEnumerable<object[]> EventData {
            get {
                return new[] {
                    new object[] { " \t", 0, null, Event.KnownTypes.Log }, 
                    new object[] { "simple string", 1, new [] { "simple string" }, Event.KnownTypes.Log }, 
                    new object[] { " \r\nsimple string", 1, new [] { "simple string" }, Event.KnownTypes.Log }, 
                    new object[] { "{simple string", 1, new [] { "{simple string" }, Event.KnownTypes.Log },
                    new object[] { "{simple string,simple string}", 1, new [] { "{simple string,simple string}" }, Event.KnownTypes.Log },
                    new object[] { "{ \"name\": \"value\" }", 1, new string[] { null }, Event.KnownTypes.Log },
                    new object[] { "{ \"message\": \"simple string\" }", 1, new [] { "simple string" }, Event.KnownTypes.Log },
                    new object[] { "{ \"message\": \"simple string\", \"data\": { \"" + Event.KnownDataKeys.Error + "\": {} } }", 1, new [] { "simple string" }, Event.KnownTypes.Error },
                    new object[] { "[simple string", 1, new [] { "[simple string" }, Event.KnownTypes.Log },
                    new object[] { "[simple string,simple string]", 1, new [] { "[simple string,simple string]" }, Event.KnownTypes.Log },
                    new object[] { "simple string\r\nsimple string", 2, new [] { "simple string", "simple string" }, Event.KnownTypes.Log }
                };
            }
        }

        [Theory]
        [PropertyData("EventData")]
        public void ParseEvents(string input, int expectedEvents, string[] expectedMessage, string expectedType) {
            var events = _eventParserPluginManager.ParseEvents(input, 2, "exceptionless/2.0.0.0");
            Assert.Equal(expectedEvents, events.Count);
            for (int index = 0; index < events.Count; index++) {
                var ev = events[index];
                Assert.Equal(expectedMessage[index], ev.Message);
                Assert.Equal(expectedType, ev.Type);
                Assert.NotEqual(DateTimeOffset.MinValue, ev.Date);
            }
        }

        [Theory]
        [PropertyData("Events")]
        public void VerifyEventParserSerialization(string eventsFilePath) {
            var json = File.ReadAllText(eventsFilePath);

            var events = _eventParserPluginManager.ParseEvents(json, 2, "exceptionless/2.0.0.0");
            Assert.Equal(1, events.Count);

            ApprovalsUtility.VerifyFile(eventsFilePath, events.First().ToJson(Formatting.Indented, IoC.GetInstance<JsonSerializerSettings>()));
        }

        [Theory]
        [PropertyData("Events")]
        public void CanDeserializeEvents(string eventsFilePath) {
            var json = File.ReadAllText(eventsFilePath);

            PersistentEvent ev = null;
            Assert.DoesNotThrow(() => { ev = json.FromJson<PersistentEvent>(IoC.GetInstance<JsonSerializerSettings>()); });
            Assert.NotNull(ev);
        }

        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\Search\Data\", "event*.json", SearchOption.AllDirectories))
                    result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}