using Exceptionless.Core.Models;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class PersistentEventExtensionsTests
{
    [Fact]
    public void SetEventReference_EmptyName_Throws()
    {
        var ev = new PersistentEvent();

        var exception = Assert.Throws<ArgumentException>(() => ev.SetEventReference(String.Empty, "reference-id"));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData("invalid_name")]
    [InlineData("abcdefghijklmnopqrstuvwxyz")]
    public void SetEventReference_LegacyCustomName_PreservesValueWithoutIndexing(string name)
    {
        var ev = new PersistentEvent();

        ev.SetEventReference(name, "reference-id");
        ev.CopyDataToIndex();

        Assert.Equal("reference-id", ev.GetEventReference(name));
        Assert.NotNull(ev.Idx);
        Assert.Empty(ev.Idx);
    }
}
