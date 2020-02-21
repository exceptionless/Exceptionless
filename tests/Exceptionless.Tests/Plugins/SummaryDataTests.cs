using System.Collections.Generic;
using System.IO;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Plugins {
    public class SummaryDataTests : TestWithServices {
        public SummaryDataTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {}

        [Theory]
        [MemberData(nameof(Events))]
        public void EventSummaryData(string path) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            string json = File.ReadAllText(path);

            var ev = json.FromJson<PersistentEvent>(settings);
            Assert.NotNull(ev);

            var data = GetService<FormattingPluginManager>().GetEventSummaryData(ev);
            var summary = new EventSummaryModel {
                TemplateKey = data.TemplateKey,
                Id = ev.Id,
                Date = ev.Date,
                Data = data.Data
            };

            string expectedContent = File.ReadAllText(Path.ChangeExtension(path, "summary.json"));
            Assert.Equal(expectedContent, JsonConvert.SerializeObject(summary, settings));
        }

        [Theory]
        [MemberData(nameof(Stacks))]
        public void StackSummaryData(string path) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            string json = File.ReadAllText(path);
            var stack = json.FromJson<Stack>(settings);
            Assert.NotNull(stack);

            var data = GetService<FormattingPluginManager>().GetStackSummaryData(stack);
            var summary = new StackSummaryModel {
                TemplateKey = data.TemplateKey,
                Data = data.Data,
                Id = stack.Id,
                Title = stack.Title,
                Total = 1,
            };

            string expectedContent = File.ReadAllText(Path.ChangeExtension(path, "summary.json"));
            Assert.Equal(expectedContent, JsonConvert.SerializeObject(summary, settings));
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