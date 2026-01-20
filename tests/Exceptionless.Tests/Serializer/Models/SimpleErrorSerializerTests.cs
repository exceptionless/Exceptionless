using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests SimpleError serialization through ITextSerializer.
/// Validates serialization and deserialization with full JSON string assertions.
/// </summary>
public class SimpleErrorSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public SimpleErrorSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_WithCompleteSimpleError_PreservesAllProperties()
    {
        // Arrange
        var error = new SimpleError
        {
            Message = "Test error message",
            Type = "System.InvalidOperationException",
            StackTrace = "   at MyApp.Services.UserService.GetUserAsync() in C:\\src\\UserService.cs:line 42\n   at MyApp.Controllers.UserController.Get() in C:\\src\\UserController.cs:line 15"
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Test error message", deserialized.Message);
        Assert.Equal("System.InvalidOperationException", deserialized.Type);
        Assert.Contains("UserService.GetUserAsync", deserialized.StackTrace);
    }

    [Fact]
    public void SerializeToString_WithMinimalSimpleError_PreservesProperties()
    {
        // Arrange
        var error = new SimpleError
        {
            Message = "Error occurred",
            Type = "CustomError"
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Error occurred", deserialized.Message);
        Assert.Equal("CustomError", deserialized.Type);
    }

    [Fact]
    public void SerializeToString_WithNestedInner_PreservesNestedStructure()
    {
        // Arrange
        var error = new SimpleError
        {
            Message = "Outer error",
            Type = "OuterException",
            Inner = new SimpleError
            {
                Message = "Inner error",
                Type = "InnerException"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Outer error", deserialized.Message);
        Assert.NotNull(deserialized.Inner);
        Assert.Equal("Inner error", deserialized.Inner.Message);
    }

    [Fact]
    public void SerializeToString_WithCustomData_PreservesDataProperty()
    {
        // Arrange
        var error = new SimpleError
        {
            Message = "Error with context",
            Type = "ContextualError",
            Data = new DataDictionary
            {
                ["user_id"] = "12345",
                ["request_id"] = "abc-123"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("12345", deserialized.Data["user_id"]);
    }

    [Fact]
    public void SerializeToString_WithMultilineStackTrace_PreservesNewlines()
    {
        // Arrange
        var error = new SimpleError
        {
            Message = "Multiline error",
            Type = "Exception",
            StackTrace = "   at Method1()\n   at Method2()\n   at Method3()"
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("   at Method1()\n   at Method2()\n   at Method3()", deserialized.StackTrace);
    }

    [Fact]
    public void Deserialize_WithCompleteJson_ReturnsPopulatedModel()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"External error","type":"System.ArgumentNullException","stack_trace":"at External.Method()","data":{"param":"value"}}""";

        // Act
        var error = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("External error", error.Message);
        Assert.Equal("System.ArgumentNullException", error.Type);
        Assert.Equal("at External.Method()", error.StackTrace);
    }

    [Fact]
    public void Deserialize_WithMinimalJson_ReturnsModelWithMessageAndType()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Test exception","type":"System.Exception"}""";

        // Act
        var error = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("Test exception", error.Message);
        Assert.Equal("System.Exception", error.Type);
        Assert.Null(error.StackTrace);
    }

    [Fact]
    public void Deserialize_WithStackTrace_ReturnsModelWithStackTrace()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Test exception","type":"System.Exception","stack_trace":"at Test.Method() in test.cs:line 10"}""";

        // Act
        var error = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("Test exception", error.Message);
        Assert.Equal("System.Exception", error.Type);
        Assert.Equal("at Test.Method() in test.cs:line 10", error.StackTrace);
    }

    [Fact]
    public void Deserialize_WithNestedInner_ReturnsModelWithInnerError()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Outer error","type":"OuterException","inner":{"message":"Inner error","type":"InnerException"}}""";

        // Act
        var error = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("Outer error", error.Message);
        Assert.NotNull(error.Inner);
        Assert.Equal("Inner error", error.Inner.Message);
    }

    [Fact]
    public void Deserialize_WithCustomData_ReturnsModelWithData()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Error with context","type":"ContextualError","data":{"user_id":"12345","request_id":"abc-123"}}""";

        // Act
        var error = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.NotNull(error.Data);
        Assert.Equal("12345", error.Data["user_id"]);
        Assert.Equal("abc-123", error.Data["request_id"]);
    }

    [Fact]
    public void Deserialize_WithMultilineStackTrace_PreservesNewlines()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"Multiline error","type":"Exception","stack_trace":"   at Method1()\n   at Method2()\n   at Method3()"}""";

        // Act
        var error = _serializer.Deserialize<SimpleError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("   at Method1()\n   at Method2()\n   at Method3()", error.StackTrace);
    }
}
