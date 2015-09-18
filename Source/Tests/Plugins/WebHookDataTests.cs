using System;
using System.Collections.Generic;
using System.IO;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Tests.Utility;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Api.Tests.Plugins {
    [UseReporter(typeof(HappyDiffReporter))]
    public class WebHookDataTests {
        private readonly WebHookDataPluginManager _webHookDataPluginManager = IoC.GetInstance<WebHookDataPluginManager>();
        private readonly FormattingPluginManager _formattingPluginManager = IoC.GetInstance<FormattingPluginManager>();

        [Theory]
        [MemberData("WebHookData")]
        public void CreateFromEvent(Version version, bool expectData) {
            var settings = IoC.GetInstance<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;
            var data = _webHookDataPluginManager.CreateFromEvent(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = $@"..\..\Plugins\WebHookData\v{version}.event.expected.json";
                ApprovalsUtility.VerifyFile(filePath, JsonConvert.SerializeObject(data, settings));
            } else {
                Assert.Null(data);
            }
        }

        [Theory]
        [MemberData("WebHookData")]
        public void CanCreateFromStack(Version version, bool expectData) {
            var settings = IoC.GetInstance<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;
            var data = _webHookDataPluginManager.CreateFromStack(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = $@"..\..\Plugins\WebHookData\v{version}.stack.expected.json";
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
            var json = File.ReadAllText(Path.GetFullPath(@"..\..\ErrorData\1477.expected.json"));

            var settings = IoC.GetInstance<JsonSerializerSettings>();
            settings.Formatting = Formatting.Indented;

            var ev = JsonConvert.DeserializeObject<PersistentEvent>(json, settings);
            ev.OrganizationId = TestConstants.OrganizationId;
            ev.ProjectId = TestConstants.ProjectId;
            ev.StackId = TestConstants.StackId;
            ev.Id = TestConstants.EventId;

            var context = new WebHookDataContext(version, ev, OrganizationData.GenerateSampleOrganization(), ProjectData.GenerateSampleProject());
            context.Stack = StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, title: _formattingPluginManager.GetStackTitle(ev), signatureHash: "722e7afd4dca4a3c91f4d94fec89dfdc");
            context.Stack.Tags = new TagSet { "Test" };
            context.Stack.FirstOccurrence = context.Stack.LastOccurrence = ev.Date.DateTime;

            return context;
        }
    }
}