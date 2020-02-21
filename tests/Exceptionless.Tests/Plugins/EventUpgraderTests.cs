using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventUpgrader;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Plugins {
    public sealed class EventUpgraderTests : TestWithServices {
        private readonly EventUpgraderPluginManager _upgrader;
        private readonly EventParserPluginManager _parser;

        public EventUpgraderTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _upgrader = GetService<EventUpgraderPluginManager>();
            _parser = GetService<EventParserPluginManager>();
        }

        [Theory]
        [MemberData(nameof(Errors))]
        public void ParseErrors(string errorFilePath) {
            string json = File.ReadAllText(errorFilePath);
            var ctx = new EventUpgraderContext(json);

            _upgrader.Upgrade(ctx);
            string expectedContent = File.ReadAllText(Path.ChangeExtension(errorFilePath, ".expected.json"));
            Assert.Equal(expectedContent, ctx.Documents.First.ToString());

            var events = _parser.ParseEvents(ctx.Documents.ToString(), 2, "exceptionless/2.0.0.0");
            Assert.Single(events);
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "ErrorData"), "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}