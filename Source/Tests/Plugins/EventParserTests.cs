using System;
using System.Collections.Generic;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Models;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
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
    }
}