using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.EventUpgrader;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(SmartReporter))]
    public class EventUpgraderTests {
        private readonly EventUpgraderPluginManager _eventUpgraderPluginManager = IoC.GetInstance<EventUpgraderPluginManager>();

        [Theory]
        [PropertyData("Errors")]
        public void ParseEvents(string errorFilePath) {
            var json = File.ReadAllText(errorFilePath);
            var ctx = new EventUpgraderContext(json);

            _eventUpgraderPluginManager.Upgrade(ctx);

            ApprovalsUtility.VerifyFile(Path.ChangeExtension(errorFilePath, ".expected.json"), ctx.Document.ToString());
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "667.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}