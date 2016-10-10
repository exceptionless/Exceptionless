using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Plugins {
    public class ManualStackingTests : TestBase {
        public ManualStackingTests(ITestOutputHelper output) : base(output) {}

        [Theory]
        [MemberData("StackingData")]
        public async Task AddManualStackSignatureData(string stackingKey, bool willAddManualStackSignature) {
            var ev = new PersistentEvent();
            ev.SetManualStackingKey(stackingKey);

            var context = new EventContext(ev);
            var plugin = new ManualStackingPlugin();
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