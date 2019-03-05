using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Tests.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Plugins {
    public class ManualStackingTests : TestWithServices {
        public ManualStackingTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {}

        [Theory]
        [MemberData(nameof(StackingData))]
        public async Task AddManualStackSignatureData(string stackingKey, bool willAddManualStackSignature) {
            var ev = new PersistentEvent();
            ev.SetManualStackingKey(stackingKey);

            var context = new EventContext(ev, OrganizationData.GenerateSampleOrganization(GetService<BillingManager>(), GetService<BillingPlans>()), ProjectData.GenerateSampleProject());
            var plugin = GetService<ManualStackingPlugin>();
            await plugin.EventBatchProcessingAsync(new List<EventContext> { context });
            Assert.Equal(willAddManualStackSignature, context.StackSignatureData.Count > 0);
        }

        public static IEnumerable<object[]> StackingData => new List<object[]> {
            new object[] { "ManualStackData", true },
            new object[] { null, false },
            new object[] { String.Empty, false }
        }.ToArray();
    }
}