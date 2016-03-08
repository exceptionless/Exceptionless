using System;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Api.Utility;
using Foundatio.Logging;
using Foundatio.Logging.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class DeltaTests : TestWithLoggingBase {
        public DeltaTests(ITestOutputHelper output) : base(output) {}

        [Fact]
        public void CanSetUnknownProperties() {
            dynamic delta = new Delta<SimpleMessageA>(Log.CreateLogger<Delta<SimpleMessageA>>());
            delta.Data = "Blah";
            delta.SomeUnknown = "yes";
            Assert.Equal(1, delta.UnknownProperties.Count);
        }

        [Fact]
        public void CanPatchUnrelatedTypes() {
            dynamic delta = new Delta<SimpleMessageA>(Log.CreateLogger<Delta<SimpleMessageA>>());
            delta.Data = "Blah";

            var msg = new SimpleMessageB {
                Data = "Blah2"
            };
            delta.Patch(msg);

            Assert.Equal(delta.Data, msg.Data);
        }

        public class SimpleMessageA {
            public string Data { get; set; }
        }

        public class SimpleMessageB {
            public string Data { get; set; }
        }
    }
}
