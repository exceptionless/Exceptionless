using System;
using System.Collections.Generic;
using System.IO;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Stats;
using Exceptionless.Core.Plugins.Formatting;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(HappyDiffReporter))]
    public class SummaryDataTests {
        private readonly FormattingPluginManager _formattingPluginManager = IoC.GetInstance<FormattingPluginManager>();

        [Theory]
        [MemberData("Events")]
        public void EventSummaryData(string path) {
            var settings = IoC.GetInstance<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            var json = File.ReadAllText(path);

            var ev = json.FromJson<PersistentEvent>(settings);
            Assert.NotNull(ev);

            var data = _formattingPluginManager.GetEventSummaryData(ev);
            var summary = new EventSummaryModel {
                TemplateKey = data.TemplateKey,
                Id = ev.Id,
                Date = ev.Date,
                Data = data.Data
            };

            ApprovalsUtility.VerifyFile(Path.ChangeExtension(path, "summary.json"), JsonConvert.SerializeObject(summary, settings));
        }

        [Theory]
        [MemberData("Stacks")]
        public void StackSummaryData(string path) {
            var settings = IoC.GetInstance<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            var json = File.ReadAllText(path);

            var stack = json.FromJson<Stack>(settings);
            Assert.NotNull(stack);

            var data = _formattingPluginManager.GetStackSummaryData(stack);
            var summary = new StackSummaryModel {
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                Id = stack.Id,
                Title = stack.Title,
                New = 1,
                Total = 1,
                Unique = 1,
                Timeline = new List<EventTermTimelineItem>()
            };

            ApprovalsUtility.VerifyFile(Path.ChangeExtension(path, "summary.json"), JsonConvert.SerializeObject(summary, settings));
        }

        public static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\Search\Data\", "event*.json", SearchOption.AllDirectories))
                    if (!file.EndsWith("summary.json"))
                        result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }

        public static IEnumerable<object[]> Stacks {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\Search\Data\", "stack*.json", SearchOption.AllDirectories))
                    if (!file.EndsWith("summary.json"))
                        result.Add(new object[] { Path.GetFullPath(file) });

                return result.ToArray();
            }
        }
    }
}