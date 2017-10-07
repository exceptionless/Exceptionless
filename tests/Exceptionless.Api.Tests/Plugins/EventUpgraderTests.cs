using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventUpgrader;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(DiffReporter))]
    public class EventUpgraderTests : TestBase {
        private readonly EventUpgraderPluginManager _upgrader;
        private readonly EventParserPluginManager _parser;

        public EventUpgraderTests(ITestOutputHelper output) : base(output) {
            _upgrader = GetService<EventUpgraderPluginManager>();
            _parser = GetService<EventParserPluginManager>();
        }

        [Theory]
        [MemberData(nameof(Errors))]
        public async Task ParseErrorsAsync(string errorFilePath) {
            string json = File.ReadAllText(errorFilePath);
            var ctx = new EventUpgraderContext(json);

            await _upgrader.UpgradeAsync(ctx);
            ApprovalsUtility.VerifyFile(Path.ChangeExtension(errorFilePath, ".expected.json"), ctx.Documents.First.ToString());

            var events = await _parser.ParseEventsAsync(ctx.Documents.ToString(), 2, "exceptionless/2.0.0.0");
            Assert.Single(events);
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (string file in Directory.GetFiles(@"..\..\..\ErrorData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}