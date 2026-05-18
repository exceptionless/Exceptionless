using System.Text.Json.Nodes;
using Exceptionless.Core.Extensions;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class JsonNodeExtensionsTests
{
    [Fact]
    public void Rename_WhenNewNameAlreadyExists_OverwritesWithRenamedValue()
    {
        // Arrange: "old_name" → "existing" but "existing" already has a value
        var obj = new JsonObject
        {
            ["old_name"] = "renamed_value",
            ["existing"] = "original_value",
            ["other"] = "keep_me"
        };

        // Act: should not throw
        bool result = obj.Rename("old_name", "existing");

        // Assert: renamed value wins, old "existing" is overwritten
        Assert.True(result);
        Assert.False(obj.ContainsKey("old_name"));
        Assert.Equal("renamed_value", obj["existing"]?.GetValue<string>());
        Assert.Equal("keep_me", obj["other"]?.GetValue<string>());
    }

    [Fact]
    public void Rename_WhenNewNameDoesNotExist_RenamesNormally()
    {
        var obj = new JsonObject
        {
            ["old_name"] = "value",
            ["other"] = "keep"
        };

        bool result = obj.Rename("old_name", "new_name");

        Assert.True(result);
        Assert.False(obj.ContainsKey("old_name"));
        Assert.Equal("value", obj["new_name"]?.GetValue<string>());
        Assert.Equal("keep", obj["other"]?.GetValue<string>());
    }

    [Fact]
    public void Rename_SameNameNoOp_ReturnsTrue()
    {
        var obj = new JsonObject { ["name"] = "value" };

        bool result = obj.Rename("name", "name");

        Assert.True(result);
        Assert.Equal("value", obj["name"]?.GetValue<string>());
    }

    [Fact]
    public void Rename_PropertyNotFound_ReturnsFalse()
    {
        var obj = new JsonObject { ["other"] = "value" };

        bool result = obj.Rename("missing", "new_name");

        Assert.False(result);
        Assert.False(obj.ContainsKey("new_name"));
    }

    [Fact]
    public void RenameOrRemoveIfNullOrEmpty_WhenNewNameAlreadyExists_OverwritesWithRenamedValue()
    {
        var obj = new JsonObject
        {
            ["old_name"] = "renamed_value",
            ["existing"] = "original_value"
        };

        bool result = obj.RenameOrRemoveIfNullOrEmpty("old_name", "existing");

        Assert.True(result);
        Assert.False(obj.ContainsKey("old_name"));
        Assert.Equal("renamed_value", obj["existing"]?.GetValue<string>());
    }

    [Fact]
    public void RenameOrRemoveIfNullOrEmpty_NullValue_RemovesProperty()
    {
        var obj = new JsonObject
        {
            ["old_name"] = null,
            ["other"] = "keep"
        };

        bool result = obj.RenameOrRemoveIfNullOrEmpty("old_name", "new_name");

        Assert.False(result);
        Assert.False(obj.ContainsKey("old_name"));
        Assert.False(obj.ContainsKey("new_name"));
        Assert.Equal("keep", obj["other"]?.GetValue<string>());
    }
}
