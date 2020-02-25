using Exceptionless.Web.Utility;
using Xunit;

namespace Exceptionless.Tests.Miscellaneous {
    public class DeltaTests {
        [Fact]
        public void CanSetUnknownProperties() {
            dynamic delta = new Delta<SimpleMessageA>();
            delta.Data = "Blah";
            delta.SomeUnknown = "yes";
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

        public class SimpleMessageA {
            public string Data { get; set; }
        }

        public class SimpleMessageB {
            public string Data { get; set; }
        }
    }
}
