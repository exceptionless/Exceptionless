using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Pipeline;

public sealed class CopySimpleDataToIdxActionTests
{
    [Theory]
    [InlineData("@ref:parent")]
    [InlineData("@ref:Parent")]
    [InlineData(" @ref:parent ")]
    [InlineData("\t@ref:Parent\r\n")]
    [InlineData("\u00A0@ref:Parent\u2003")]
    public async Task ProcessAsync_FreeParentReference_UsesNormalIndexKeyNormalization(string key)
    {
        var ev = new PersistentEvent
        {
            Data = new() { [key] = "parent-reference", ["custom"] = "not indexed" }
        };
        var context = new EventContext(ev, new Organization { HasPremiumFeatures = false }, new Project());
        var action = new CopySimpleDataToIdxAction(new AppOptions { DisabledPipelineActions = [] }, NullLoggerFactory.Instance);

        await action.ProcessAsync(context);

        Assert.NotNull(ev.Idx);
        Assert.Equal("parent-reference", Assert.Single(ev.Idx).Value);
        Assert.True(ev.Idx.ContainsKey("parent-r"));
    }

    [Fact]
    public async Task ProcessAsync_FreeEventWithoutParent_DoesNotIndexCustomData()
    {
        var ev = new PersistentEvent
        {
            Data = new() { ["@ref:custom"] = "custom-reference", ["custom"] = "not indexed" }
        };
        var context = new EventContext(ev, new Organization { HasPremiumFeatures = false }, new Project());
        var action = new CopySimpleDataToIdxAction(new AppOptions { DisabledPipelineActions = [] }, NullLoggerFactory.Instance);

        await action.ProcessAsync(context);

        Assert.Null(ev.Idx);
    }

    [Theory]
    [InlineData("last-parent-reference")]
    [InlineData(null)]
    public async Task ProcessAsync_DuplicateNormalizedParentKeys_PreservesLastValue(string? lastValue)
    {
        var ev = new PersistentEvent
        {
            Data = new() { ["@ref:parent"] = "first-parent-reference", [" @ref:Parent "] = lastValue }
        };
        var context = new EventContext(ev, new Organization { HasPremiumFeatures = false }, new Project());
        var action = new CopySimpleDataToIdxAction(new AppOptions { DisabledPipelineActions = [] }, NullLoggerFactory.Instance);

        await action.ProcessAsync(context);

        Assert.NotNull(ev.Idx);
        Assert.Equal(lastValue, ev.Idx["parent-r"]);
    }
}
