using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Xunit;

namespace Exceptionless.Api.Tests.Plugins {
    public class ManualStackingTests {

        [Theory]
        [MemberData("EventData")]
        public async Task AddManualStackSignatureData(string dataKey, string dataValue, bool willAddManualStackSignature) {
            var plugin = new ManualStackingPlugin();
            var data = new DataDictionary() { { dataKey, dataValue } };
            var context = new EventContext(new PersistentEvent { Data = data });

            await plugin.EventBatchProcessingAsync(new List<EventContext> { context });

            Assert.Equal(willAddManualStackSignature, context.StackSignatureData.Count > 0);
        }

        public static IEnumerable<object[]> EventData => new List<object[]> {
            new object[] { Event.KnownDataKeys.ManualStackingKey, "ManualStackData", true },
            new object[] { Event.KnownDataKeys.ManualStackingKey, null, false },
            new object[] { Event.KnownDataKeys.ManualStackingKey, string.Empty, false },
            new object[] { "NotAManualStackingKey", "ManualStackData", false }
        }.ToArray();
    }
}