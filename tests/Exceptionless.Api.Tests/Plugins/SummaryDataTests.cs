using System;
using System.Collections.Generic;
using System.IO;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(DiffReporter))]
    public class SummaryDataTests : TestBase {
        private readonly FormattingPluginManager _formatter;

        public SummaryDataTests(ITestOutputHelper output) : base(output) {
            _formatter = GetService<FormattingPluginManager>();
        }

#if DEBUG
        [Theory]
        [MemberData(nameof(Events))]
#endif
        public void EventSummaryData(string path) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            string json = File.ReadAllText(path);

            var ev = json.FromJson<PersistentEvent>(settings);
            Assert.NotNull(ev);

            var data = _formatter.GetEventSummaryData(ev);
            var summary = new EventSummaryModel {
                TemplateKey = data.TemplateKey,
                Id = ev.Id,
                Date = ev.Date,
                Data = data.Data
            };

            ApprovalsUtility.VerifyFile(Path.ChangeExtension(path, "summary.json"), JsonConvert.SerializeObject(summary, settings));
        }

#if DEBUG
        [Theory]
        [MemberData(nameof(Stacks))]
#endif
        public void StackSummaryData(string path) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            string json = File.ReadAllText(path);
            var stack = json.FromJson<Stack>(settings);
            Assert.NotNull(stack);

            var data = _formatter.GetStackSummaryData(stack);
            var summary = new StackSummaryModel {
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                Id = stack.Id,
                Title = stack.Title,
                Total = 1,
            };

            ApprovalsUtility.VerifyFile(Path.ChangeExtension(path, "summary.json"), JsonConvert.SerializeObject(summary, settings));
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

        public static IEnumerable<object[]> Stacks {
            get {
                var result = new List<object[]>();
                foreach (string file in Directory.GetFiles(Path.Combine("..", "..", "..", "Search", "Data"), "stack*.json", SearchOption.AllDirectories))
                    if (!file.EndsWith("summary.json"))
                        result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}