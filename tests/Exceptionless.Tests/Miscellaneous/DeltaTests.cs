using Exceptionless.Web.Utility;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Miscellaneous;

public class DeltaTests : TestWithLoggingBase
{
    public DeltaTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TrySetPropertyValue_UnknownProperty_AddsToUnknownProperties()
    {
        dynamic delta = new Delta<SimpleMessageA>();
        delta.Data = "Blah";
        delta.SomeUnknown = "yes";
        Assert.Equal(1, delta.UnknownProperties.Count);
    }

    [Fact]
    public void Patch_UnrelatedTypes_CopiesMatchingProperties()
    {
        dynamic delta = new Delta<SimpleMessageA>();
        delta.Data = "Blah";

        var msg = new SimpleMessageB
        {
            Data = "Blah2"
        };
        delta.Patch(msg);

        Assert.Equal(delta.Data, msg.Data);
    }

    public record SimpleMessageA
    {
        public required string Data { get; set; }
    }

    public record SimpleMessageB
    {
        public required string Data { get; set; }
    }
}
