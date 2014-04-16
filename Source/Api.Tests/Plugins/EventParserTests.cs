using System;
using System.Collections.Generic;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.EventParserPlugins;
using Exceptionless.Models;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    public class EventParserTests {
        private readonly EventParserPluginManager _eventParserPluginManager = IoC.GetInstance<EventParserPluginManager>();

        public static IEnumerable<object[]> EventData {
            get {
                return new[] {
                    new object[] { " \t", 0, Event.KnownTypes.Log }, 
                    new object[] { "simple string", 1, Event.KnownTypes.Log }, 
                    new object[] { " \r\nsimple string", 1, Event.KnownTypes.Log }, 
                    new object[] { "{simple string", 1, Event.KnownTypes.Log },
                    new object[] { "{simple string,simple string}", 1, Event.KnownTypes.Log },
                    new object[] { "{ \"name\": \"value\" }", 1, Event.KnownTypes.Log },
                    new object[] { "{ \"message\": \"simple string\" }", 1, Event.KnownTypes.Log },
                    new object[] { "[simple string", 1, Event.KnownTypes.Log },
                    new object[] { "[simple string,simple string]", 1, Event.KnownTypes.Log },
                    new object[] { "simple string\r\nsimple string", 2, Event.KnownTypes.Log }, 
                };
            }
        }

        [Theory]
        [PropertyData("EventData")]
        public void ParseEvents(string input, int expectedEvents, string expectedType) {
            var events = _eventParserPluginManager.ParseEvents(input);
            Assert.Equal(expectedEvents, events.Count);
            foreach (var ev in events) {
                Assert.Equal(expectedType, ev.Type);
                Assert.False(String.IsNullOrWhiteSpace(ev.Message));
                Assert.NotEqual(ev.Date, DateTimeOffset.MinValue);
            }
        }
    }
}