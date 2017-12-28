using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Tests.Utility;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(DiffReporter))]
    public class WebHookDataTests : TestBase {
        private readonly WebHookDataPluginManager _webHookData;
        private readonly FormattingPluginManager _formatter;

        public WebHookDataTests(ITestOutputHelper output) : base(output) {
            _webHookData = GetService<WebHookDataPluginManager>();
            _formatter = GetService<FormattingPluginManager>();
        }

#if DEBUG
        [Theory]
        [MemberData(nameof(WebHookData))]
#endif
        public async Task CreateFromEventAsync(Version version, bool expectData) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;
            object data = await _webHookData.CreateFromEventAsync(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = Path.GetFullPath(Path.Combine("..", "..", "..", "Plugins", "WebHookData", $"v{version}.event.expected.json"));
                ApprovalsUtility.VerifyFile(filePath, JsonConvert.SerializeObject(data, settings));
            } else {
                Assert.Null(data);
            }
        }

#if DEBUG
        [Theory]
        [MemberData(nameof(WebHookData))]
#endif
        public async Task CanCreateFromStackAsync(Version version, bool expectData) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;
            object data = await _webHookData.CreateFromStackAsync(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = Path.GetFullPath(Path.Combine("..", "..", "..", "Plugins", "WebHookData", $"v{version}.stack.expected.json"));
                ApprovalsUtility.VerifyFile(filePath, JsonConvert.SerializeObject(data, settings));
            } else {
                Assert.Null(data);
            }
        }

        public static IEnumerable<object[]> WebHookData => new List<object[]> {
            new object[] { new Version(0, 0), false },
            new object[] { new Version(1, 0), true },
            new object[] { new Version(2, 0), true },
            new object[] { new Version(3, 0), false }
        }.ToArray();

        private WebHookDataContext GetWebHookDataContext(Version version) {
            string json = File.ReadAllText(Path.GetFullPath(Path.Combine("..", "..", "..", "ErrorData", "1477.expected.json")));

            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            var ev = JsonConvert.DeserializeObject<PersistentEvent>(json, settings);
            ev.OrganizationId = TestConstants.OrganizationId;
            ev.ProjectId = TestConstants.ProjectId;
            ev.StackId = TestConstants.StackId;
            ev.Id = TestConstants.EventId;

            var context = new WebHookDataContext(version, ev, OrganizationData.GenerateSampleOrganization(), ProjectData.GenerateSampleProject()) {
                Stack = StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, title: _formatter.GetStackTitle(ev), signatureHash: "722e7afd4dca4a3c91f4d94fec89dfdc")
            };
            context.Stack.Tags = new TagSet { "Test" };
            context.Stack.FirstOccurrence = context.Stack.LastOccurrence = ev.Date.UtcDateTime;

            return context;
        }
    }
}