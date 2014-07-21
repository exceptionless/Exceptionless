using System;
using System.Collections.Generic;
using System.IO;
using ApprovalTests.Reporters;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Exceptionless.Tests.Utility;
using Newtonsoft.Json;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins
{
    [UseReporter(typeof(SmartReporter))]
    public class WebHookDataTests {
        private readonly WebHookDataPluginManager _webHookDataPluginManager = IoC.GetInstance<WebHookDataPluginManager>();

        // Figure out what the hook payload will look like and how we will handle old clients. 
        // I think we are going to need to have each webhook subscription have a schema version associated to it.

        // Allow setting the schema for a web hook when creating it and also allow it to be changed.

        [Fact]
        public void CanHandleOrganizationWebHook() {
            // Make sure that they work at both the project and org levels.
            throw new NotImplementedException();
        }

        [Fact]
        public void CanHandleProjectWebHook() {
            // Make sure that they work at both the project and org levels.
            throw new NotImplementedException();
        }

        [Theory]
        [PropertyData("WebHookData")]
        public void CreateFromEvent(Version version, bool expectData) {
            var data = _webHookDataPluginManager.CreateFromEvent(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = String.Format(@"..\..\Plugins\WebHookData\v{0}.event.expected.json", version);
                ApprovalsUtility.VerifyFile(filePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            } else {
                Assert.Null(data);
            }
        }

        [Theory]
        [PropertyData("WebHookData")]
        public void CanCreateFromStack(Version version, bool expectData) {
            var data = _webHookDataPluginManager.CreateFromStack(GetWebHookDataContext(version));
            if (expectData) {
                string filePath = String.Format(@"..\..\Plugins\WebHookData\v{0}.stack.expected.json", version);
                ApprovalsUtility.VerifyFile(filePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            } else {
                Assert.Null(data);
            }
        }

        public static IEnumerable<object[]> WebHookData {
            get {
                return new List<object[]> {
                    new object[] { new Version(0, 0), false }, 
                    new object[] { new Version(1, 0), true }, 
                    new object[] { new Version(2, 0), true }, 
                    new object[] { new Version(3, 0), false }
                }.ToArray();
            }
        }

        private WebHookDataContext GetWebHookDataContext(Version version) {
            var json = File.ReadAllText(Path.GetFullPath(@"..\..\ErrorData\1477.expected.json"));

            var settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ContractResolver = new ExtensionContractResolver()
            };

            var ev = JsonConvert.DeserializeObject<PersistentEvent>(json, settings);
            ev.Id = TestConstants.EventId;
            ev.OrganizationId = TestConstants.OrganizationId;
            ev.ProjectId = TestConstants.ProjectId;
            ev.StackId = TestConstants.StackId;

            var context = new WebHookDataContext(version, ev, OrganizationData.GenerateSampleOrganization(), ProjectData.GenerateSampleProject());
            context.Stack = StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, title: ev.Message, signatureHash: "722e7afd4dca4a3c91f4d94fec89dfdc");
            context.Stack.Tags = new TagSet { "Test" };
            context.Stack.FirstOccurrence = context.Stack.LastOccurrence = ev.Date.DateTime;

            return context;
        }
    }
}