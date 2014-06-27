using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.EventUpgrader;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    public class EventUpgraderTests {
        private readonly EventUpgraderPluginManager _eventUpgraderPluginManager = IoC.GetInstance<EventUpgraderPluginManager>();

        [Theory]
        [PropertyData("Errors")]
        public void ParseEvents(string errorFilePath) {
            var json = File.ReadAllText(errorFilePath);
            var ctx = new EventUpgraderContext(json);

            _eventUpgraderPluginManager.Upgrade(ctx);

            string expected = Regex.Replace(File.ReadAllText(Path.ChangeExtension(errorFilePath, ".expected.json")), @"\s", "");
            Assert.Equal(Regex.Replace(ctx.Document.ToString(), @"\s", ""), expected);
        }

        public static IEnumerable<object[]> Errors {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\ErrorData\", "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }
    }
}