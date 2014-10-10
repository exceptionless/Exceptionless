using System;
using Exceptionless.Api.Tests.Messaging;
using Exceptionless.Api.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class DeltaTests {
        [Fact]
        public void CanSetUnknownProperties() {
            dynamic delta = new Delta<SimpleMessageA>();
            delta.Data = "Blah";
            Assert.DoesNotThrow(() => delta.SomeUnknown = "yes");
            Assert.Equal(1, delta.UnknownProperties.Count);
        }

        [Fact]
        public void CanPatchUnrelatedTypes() {
            dynamic delta = new Delta<SimpleMessageA>();
            delta.Data = "Blah";

            var msg = new SimpleMessageB {
                Data = "Blah2"
            };
            delta.Patch(msg);

            Assert.Equal(delta.Data, msg.Data);
        }
    }
}
