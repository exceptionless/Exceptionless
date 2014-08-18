using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Plugins.EventUpgrader;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(SmartReporter))]
    public class EventUpgraderTests {
        private readonly EventUpgraderPluginManager _eventUpgraderPluginManager = IoC.GetInstance<EventUpgraderPluginManager>();
        private readonly EventParserPluginManager _eventParserPluginManager = IoC.GetInstance<EventParserPluginManager>();

        [Theory]
        [PropertyData("Errors")]
        public void ParseEvents(string errorFilePath) {
            var json = File.ReadAllText(errorFilePath);
            var ctx = new EventUpgraderContext(json);

            // TODO: Figure out what is wrong with 800000002e519522d83837a1
            _eventUpgraderPluginManager.Upgrade(ctx);
            ApprovalsUtility.VerifyFile(Path.ChangeExtension(errorFilePath, ".expected.json"), ctx.Documents.First.ToString());

            var events = _eventParserPluginManager.ParseEvents(ctx.Documents.ToString(), 2, "exceptionless/2.0.0.0");
            Assert.Equal(1, events.Count);
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}