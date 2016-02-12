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

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(DiffReporter))]
    public class RaygunEventParserTests {
        private readonly EventParserPluginManager _eventParserPluginManager = IoC.GetInstance<EventParserPluginManager>();
        
        [Theory]
        [MemberData("Events")]
        public void VerifyEventParserSerialization(string eventsFilePath) {
            var json = File.ReadAllText(eventsFilePath);

            var events = _eventParserPluginManager.ParseEvents(json, 1, "raygun");
            Assert.Equal(1, events.Count);

            ApprovalsUtility.VerifyFile(Path.ChangeExtension(eventsFilePath, ".expected.json"), events.First().ToJson(Formatting.Indented, IoC.GetInstance<JsonSerializerSettings>()));
        }
        
        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\Plugins\EventParser\Raygun\Data\", "*.json", SearchOption.AllDirectories))
                    if (!file.EndsWith("expected.json"))
                        result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}