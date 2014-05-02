using System;
using CodeSmith.Core.Helpers;
using Exceptionless.Api.Tests.Messaging;
using Exceptionless.Core.Web;
using Xunit;

namespace Exceptionless.Api.Tests.Miscelaneous {
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

        [Fact]
        public void CanRunOnce() {
            Run.Once(TestMethod);
            Assert.Equal(1, _counter);
            Run.Once(TestMethod);
            Assert.Equal(1, _counter);
        }

        [Fact]
        public void CanRunWithRetries() {
            int attempts = 0;
            Assert.Throws(typeof(ApplicationException), () => Run.WithRetries(() => {
                attempts++;
                throw new ApplicationException();
            }));
            Assert.Equal(3, attempts);
        }

        private int _counter = 0;
        private void TestMethod() {
            _counter++;
        }
    }
}
