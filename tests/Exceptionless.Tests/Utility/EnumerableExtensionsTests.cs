using Exceptionless.Core.Extensions;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class EnumerableExtensionsTests
{
    [Fact]
    public void CollectionEquals_DifferentElements_ReturnsFalse()
    {
        // Arrange
        var source = new[] { 1, 2, 3 };
        var other = new[] { 1, 2, 4 };

        // Act
        bool result = source.CollectionEquals(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CollectionEquals_DifferentOrder_ReturnsFalse()
    {
        // Arrange
        var source = new[] { 1, 2, 3 };
        var other = new[] { 3, 2, 1 };

        // Act
        bool result = source.CollectionEquals(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CollectionEquals_SameElementsSameOrder_ReturnsTrue()
    {
        // Arrange
        var source = new[] { 1, 2, 3 };
        var other = new[] { 1, 2, 3 };

        // Act
        bool result = source.CollectionEquals(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetCollectionHashCode_SameCollections_ProduceSameHashCode()
    {
        // Arrange
        var first = new[] { "a", "b", "c" };
        var second = new[] { "a", "b", "c" };

        // Act
        int hash1 = first.GetCollectionHashCode();
        int hash2 = second.GetCollectionHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }
}
