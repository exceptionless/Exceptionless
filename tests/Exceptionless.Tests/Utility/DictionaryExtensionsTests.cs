using Exceptionless.Core.Extensions;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class DictionaryExtensionsTests
{
    [Fact]
    public void AddRange_WithNewKeys_AddsAllEntries()
    {
        // Arrange
        var target = new Dictionary<string, int> { ["a"] = 1 };
        var source = new Dictionary<string, int> { ["b"] = 2, ["c"] = 3 };

        // Act
        target.AddRange(source);

        // Assert
        Assert.Equal(3, target.Count);
        Assert.Equal(2, target["b"]);
        Assert.Equal(3, target["c"]);
    }

    [Fact]
    public void AddRange_WithExistingKey_OverwritesValue()
    {
        // Arrange
        var target = new Dictionary<string, int> { ["a"] = 1 };
        var source = new Dictionary<string, int> { ["a"] = 99 };

        // Act
        target.AddRange(source);

        // Assert
        Assert.Equal(99, target["a"]);
    }

    [Fact]
    public void CollectionEquals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var source = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var other = new Dictionary<string, int> { ["a"] = 1, ["b"] = 99 };

        // Act
        bool result = source.CollectionEquals(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CollectionEquals_NullValueComparedToNonNullValue_ReturnsFalse()
    {
        // Arrange
        var source = new Dictionary<string, string?> { ["a"] = null };
        var other = new Dictionary<string, string?> { ["a"] = "value" };

        // Act
        bool result = source.CollectionEquals(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CollectionEquals_SameKeysAndValues_ReturnsTrue()
    {
        // Arrange
        var source = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var other = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        // Act
        bool result = source.CollectionEquals(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsKeyWithValue_KeyExistsWithMatchingValue_ReturnsTrue()
    {
        // Arrange
        var dictionary = new Dictionary<string, string> { ["key"] = "value" };

        // Act
        bool result = dictionary.ContainsKeyWithValue("key", "value");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsKeyWithValue_KeyMissing_ReturnsFalse()
    {
        // Arrange
        var dictionary = new Dictionary<string, string> { ["key"] = "value" };

        // Act
        bool result = dictionary.ContainsKeyWithValue("missing", "value");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsKeyWithValue_NoValuesProvided_ReturnsFalse()
    {
        // Arrange
        var dictionary = new Dictionary<string, string> { ["key"] = "value" };

        // Act
        bool result = dictionary.ContainsKeyWithValue("key");

        // Assert
        Assert.False(result);
    }
}
