using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(DiffReporter))]
    public class EventParserTests : TestBase {
        private readonly EventParserPluginManager _parser;

        public EventParserTests(ITestOutputHelper output) : base(output) {
            _parser = GetService<EventParserPluginManager>();
        }

        public static IEnumerable<object[]> EventData => new[] {
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

        [Theory]
        [MemberData(nameof(EventData))]
        public void ParseEvents(string input, int expectedEvents, string[] expectedMessage, string expectedType) {
            var events = _parser.ParseEvents(input, 2, "exceptionless/2.0.0.0");
            Assert.Equal(expectedEvents, events.Count);
            for (int index = 0; index < events.Count; index++) {
                var ev = events[index];
                Assert.Equal(expectedMessage[index], ev.Message);
                Assert.Equal(expectedType, ev.Type);
                Assert.NotEqual(DateTimeOffset.MinValue, ev.Date);
            }
        }

#if DEBUG
        [Theory]
        [MemberData(nameof(Events))]
#endif
        public void VerifyEventParserSerialization(string eventsFilePath) {
            string json = File.ReadAllText(eventsFilePath);

            var events = _parser.ParseEvents(json, 2, "exceptionless/2.0.0.0");
            Assert.Single(events);

            ApprovalsUtility.VerifyFile(eventsFilePath, events.First().ToJson(Formatting.Indented, GetService<JsonSerializerSettings>()));
        }

        [Theory]
        [MemberData(nameof(Events))]
        public void CanDeserializeEvents(string eventsFilePath) {
            string json = File.ReadAllText(eventsFilePath);

            var ev = json.FromJson<PersistentEvent>(GetService<JsonSerializerSettings>());
            Assert.NotNull(ev);
        }

        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "Search", "Data"), "event*.json", SearchOption.AllDirectories))
                    if (!file.EndsWith("summary.json"))
                        result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}