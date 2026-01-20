using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests UserInfo serialization through ITextSerializer.
/// Validates serialization and deserialization with full JSON string assertions.
/// </summary>
public class UserInfoSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public UserInfoSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_WithCompleteUserInfo_PreservesAllProperties()
    {
        // Arrange
        var userInfo = new UserInfo("user@example.com", "Test User");
        userInfo.Data!["custom_string"] = "value";
        userInfo.Data["custom_number"] = 42;
        userInfo.Data["custom_bool"] = true;

        // Act
        string? json = _serializer.SerializeToString(userInfo);
        var deserialized = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("user@example.com", deserialized.Identity);
        Assert.Equal("Test User", deserialized.Name);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("value", deserialized.Data["custom_string"]);
    }

    [Fact]
    public void SerializeToString_WithNullName_PreservesIdentityOnly()
    {
        // Arrange
        var userInfo = new UserInfo("user@example.com");

        // Act
        string? json = _serializer.SerializeToString(userInfo);
        var deserialized = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("user@example.com", deserialized.Identity);
        Assert.Null(deserialized.Name);
    }

    [Fact]
    public void SerializeToString_WithEmptyData_PreservesBasicProperties()
    {
        // Arrange
        var userInfo = new UserInfo("test@example.com", "Test");

        // Act
        string? json = _serializer.SerializeToString(userInfo);
        var deserialized = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("test@example.com", deserialized.Identity);
        Assert.Equal("Test", deserialized.Name);
    }

    [Fact]
    public void SerializeToString_WithMixedDataTypes_PreservesAllData()
    {
        // Arrange
        var userInfo = new UserInfo("test@example.com", "User");
        userInfo.Data!["string"] = "text";
        userInfo.Data["number"] = 123;
        userInfo.Data["boolean"] = false;
        userInfo.Data["decimal"] = 3.14m;

        // Act
        string? json = _serializer.SerializeToString(userInfo);
        var deserialized = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("test@example.com", deserialized.Identity);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("text", deserialized.Data["string"]);
    }

    [Fact]
    public void SerializeToString_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var userInfo = new UserInfo("user+tag@example.com", "O'Brien, John Jr.");
        userInfo.Data!["special"] = "Value with \"quotes\" and \\backslash";

        // Act
        string? json = _serializer.SerializeToString(userInfo);
        var deserialized = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("user+tag@example.com", deserialized.Identity);
        Assert.Equal("O'Brien, John Jr.", deserialized.Name);
        Assert.Equal("Value with \"quotes\" and \\backslash", deserialized.Data!["special"]);
    }

    [Fact]
    public void SerializeToString_WithUnicodeCharacters_PreservesUnicode()
    {
        // Arrange
        var userInfo = new UserInfo("用户@example.com", "日本語ユーザー");

        // Act
        string? json = _serializer.SerializeToString(userInfo);
        var deserialized = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("用户@example.com", deserialized.Identity);
        Assert.Equal("日本語ユーザー", deserialized.Name);
    }

    [Fact]
    public void Deserialize_WithCompleteJson_ReturnsPopulatedModel()
    {
        // Arrange
        /* language=json */
        const string json = """{"identity":"parsed@example.com","name":"Parsed User","data":{"extra":"data"}}""";

        // Act
        var result = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("parsed@example.com", result.Identity);
        Assert.Equal("Parsed User", result.Name);
        Assert.NotNull(result.Data);
        Assert.Equal("data", result.Data["extra"]);
    }

    [Fact]
    public void Deserialize_WithMinimalJson_ReturnsModelWithIdentityOnly()
    {
        // Arrange
        /* language=json */
        const string json = """{"identity":"minimal@example.com"}""";

        // Act
        var result = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("minimal@example.com", result.Identity);
        Assert.Null(result.Name);
    }

    [Fact]
    public void Deserialize_WithDataProperty_ReturnsModelWithCustomData()
    {
        // Arrange
        /* language=json */
        const string json = """{"identity":"user@example.com","name":"Test User","data":{"key":"value"}}""";

        // Act
        var result = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user@example.com", result.Identity);
        Assert.Equal("Test User", result.Name);
        Assert.NotNull(result.Data);
        Assert.Equal("value", result.Data["key"]);
    }

    [Fact]
    public void Deserialize_WithoutDataProperty_ReturnsModelWithEmptyData()
    {
        // Arrange
        /* language=json */
        const string json = """{"identity":"user@example.com","name":"User"}""";

        // Act
        var result = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void Deserialize_WithSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        /* language=json */
        const string json = """{"identity":"user+tag@example.com","name":"O'Brien, John Jr.","data":{"special":"Value with \"quotes\" and \\backslash"}}""";

        // Act
        var result = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user+tag@example.com", result.Identity);
        Assert.Equal("O'Brien, John Jr.", result.Name);
        Assert.Equal("Value with \"quotes\" and \\backslash", result.Data!["special"]);
    }

    [Fact]
    public void Deserialize_WithUnicodeCharacters_PreservesUnicode()
    {
        // Arrange
        /* language=json */
        const string json = """{"identity":"用户@example.com","name":"日本語ユーザー"}""";

        // Act
        var result = _serializer.Deserialize<UserInfo>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("用户@example.com", result.Identity);
        Assert.Equal("日本語ユーザー", result.Name);
    }
}
