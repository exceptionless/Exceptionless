using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Plugins {
    public sealed class WebHookDataTests : TestWithServices {
        private readonly WebHookDataPluginManager _webHookData;
        private readonly FormattingPluginManager _formatter;

        public WebHookDataTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _webHookData = GetService<WebHookDataPluginManager>();
            _formatter = GetService<FormattingPluginManager>();
        }

        [Theory]
        [MemberData(nameof(WebHookData))]
        public async Task CreateFromEventAsync(string version, bool expectData) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;
            object data = await _webHookData.CreateFromEventAsync(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = Path.GetFullPath(Path.Combine("..", "..", "..", "Plugins", "WebHookData", $"{version}.event.expected.json"));
                string expectedContent = File.ReadAllText(filePath);
                Assert.Equal(expectedContent, JsonConvert.SerializeObject(data, settings));
            } else {
                Assert.Null(data);
            }
        }

        [Theory]
        [MemberData(nameof(WebHookData))]
        public async Task CanCreateFromStackAsync(string version, bool expectData) {
            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;
            object data = await _webHookData.CreateFromStackAsync(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = Path.GetFullPath(Path.Combine("..", "..", "..", "Plugins", "WebHookData", $"{version}.stack.expected.json"));
                string expectedContent = File.ReadAllText(filePath);
                Assert.Equal(expectedContent, JsonConvert.SerializeObject(data, settings));
            } else {
                Assert.Null(data);
            }
        }

        public static IEnumerable<object[]> WebHookData => new List<object[]> {
            new object[] { "v0", false },
            new object[] { WebHook.KnownVersions.Version1, true },
            new object[] { WebHook.KnownVersions.Version2, true },
            new object[] { "v3", false }
        }.ToArray();

        private WebHookDataContext GetWebHookDataContext(string version) {
            string json = File.ReadAllText(Path.GetFullPath(Path.Combine("..", "..", "..", "ErrorData", "1477.expected.json")));

            var settings = GetService<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            var ev = JsonConvert.DeserializeObject<PersistentEvent>(json, settings);
            ev.OrganizationId = TestConstants.OrganizationId;
            ev.ProjectId = TestConstants.ProjectId;
            ev.StackId = TestConstants.StackId;
            ev.Id = TestConstants.EventId;

            var context = new WebHookDataContext(version, ev, OrganizationData.GenerateSampleOrganization(GetService<BillingManager>(), GetService<BillingPlans>()), ProjectData.GenerateSampleProject()) {
                Stack = StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, title: _formatter.GetStackTitle(ev), signatureHash: "722e7afd4dca4a3c91f4d94fec89dfdc")
            };
            context.Stack.Tags = new TagSet { "Test" };
            context.Stack.FirstOccurrence = context.Stack.LastOccurrence = ev.Date.UtcDateTime;

            return context;
        }
    }
}