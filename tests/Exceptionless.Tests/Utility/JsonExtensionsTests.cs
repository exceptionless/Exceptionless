using Exceptionless.Core.Extensions;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class JsonExtensionsTests : TestWithLoggingBase
{
    public JsonExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GetJsonType_ArrayString_ReturnsArray()
    {
        // Arrange
        /* language=json */
        const string json = "[1, 2, 3]";

        // Act
        JsonType result = json.GetJsonType();

        // Assert
        Assert.Equal(JsonType.Array, result);
    }

    [Fact]
    public void GetJsonType_ArrayWithLeadingWhitespace_ReturnsArray()
    {
        // Arrange
        /* language=json */
        const string json = "  \t\n[1, 2, 3]";

        // Act
        JsonType result = json.GetJsonType();

        // Assert
        Assert.Equal(JsonType.Array, result);
    }

    [Fact]
    public void GetJsonType_EmptyString_ReturnsNone()
    {
        // Act
        JsonType result = "".GetJsonType();

        // Assert
        Assert.Equal(JsonType.None, result);
    }

    [Fact]
    public void GetJsonType_NullString_ReturnsNone()
    {
        // Act
        JsonType result = ((string)null!).GetJsonType();

        // Assert
        Assert.Equal(JsonType.None, result);
    }

    [Fact]
    public void GetJsonType_ObjectString_ReturnsObject()
    {
        // Arrange
        /* language=json */
        const string json = """{"key": "value"}""";

        // Act
        JsonType result = json.GetJsonType();

        // Assert
        Assert.Equal(JsonType.Object, result);
    }

    [Fact]
    public void GetJsonType_ObjectWithLeadingWhitespace_ReturnsObject()
    {
        // Arrange
        /* language=json */
        const string json = "  \t\n{\"key\": \"value\"}";

        // Act
        JsonType result = json.GetJsonType();

        // Assert
        Assert.Equal(JsonType.Object, result);
    }

    [Fact]
    public void GetJsonType_PlainText_ReturnsNone()
    {
        // Act
        JsonType result = "hello world".GetJsonType();

        // Assert
        Assert.Equal(JsonType.None, result);
    }

    [Fact]
    public void IsJson_ArrayString_ReturnsTrue()
    {
        // Arrange
        /* language=json */
        const string json = "[1, 2, 3]";

        // Act
        bool result = json.IsJson();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsJson_EmptyString_ReturnsFalse()
    {
        // Act
        bool result = "".IsJson();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsJson_ObjectString_ReturnsTrue()
    {
        // Arrange
        /* language=json */
        const string json = """{"key": "value"}""";

        // Act
        bool result = json.IsJson();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsJson_PlainText_ReturnsFalse()
    {
        // Act
        bool result = "not json".IsJson();

        // Assert
        Assert.False(result);
    }
}
