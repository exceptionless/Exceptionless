using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests Error model serialization through ITextSerializer.
/// Validates nested error structures and stack trace serialization via round-trip.
/// </summary>
public class ErrorSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ErrorSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_CompleteError_PreservesAllProperties()
    {
        // Arrange
        var error = new Error
        {
            Message = "Test exception",
            Type = "System.InvalidOperationException",
            Code = "ERR001",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "Method1",
                    DeclaringNamespace = "Namespace1",
                    DeclaringType = "Class1",
                    LineNumber = 42,
                    Column = 10
                }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Test exception", deserialized.Message);
        Assert.Equal("System.InvalidOperationException", deserialized.Type);
        Assert.Equal("ERR001", deserialized.Code);
        Assert.NotNull(deserialized.StackTrace);
        Assert.Single(deserialized.StackTrace);
        Assert.Equal("Method1", deserialized.StackTrace[0].Name);
    }

    [Fact]
    public void SerializeToString_MinimalError_PreservesProperties()
    {
        // Arrange
        var error = new Error
        {
            Message = "Test",
            Type = "TestException"
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Test", deserialized.Message);
        Assert.Equal("TestException", deserialized.Type);
    }

    [Fact]
    public void Deserialize_ErrorWithSingleStackFrame_PreservesFrame()
    {
        // Arrange
        var original = new Error
        {
            Message = "Test exception",
            Type = "System.Exception",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "ExecuteAsync",
                    DeclaringNamespace = "MyApp.Services",
                    DeclaringType = "UserService",
                    LineNumber = 42,
                    Column = 10
                }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.StackTrace);
        Assert.Single(deserialized.StackTrace);
        Assert.Equal("ExecuteAsync", deserialized.StackTrace[0].Name);
        Assert.Equal("MyApp.Services", deserialized.StackTrace[0].DeclaringNamespace);
        Assert.Equal("UserService", deserialized.StackTrace[0].DeclaringType);
        Assert.Equal(42, deserialized.StackTrace[0].LineNumber);
        Assert.Equal(10, deserialized.StackTrace[0].Column);
    }

    [Fact]
    public void Deserialize_ErrorWithMultipleStackFrames_PreservesAllFrames()
    {
        // Arrange
        var original = new Error
        {
            Message = "Test exception",
            Type = "System.Exception",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "Method1",
                    DeclaringNamespace = "Namespace1",
                    DeclaringType = "Class1",
                    LineNumber = 42
                },
                new StackFrame
                {
                    Name = "Method2",
                    DeclaringNamespace = "Namespace2",
                    DeclaringType = "Class2",
                    LineNumber = 100
                }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.StackTrace);
        Assert.Equal(2, deserialized.StackTrace.Count);
        Assert.Equal("Method1", deserialized.StackTrace[0].Name);
        Assert.Equal("Method2", deserialized.StackTrace[1].Name);
    }

    [Fact]
    public void Deserialize_ErrorWithSingleInner_PreservesHierarchy()
    {
        // Arrange
        var original = new Error
        {
            Message = "Outer exception",
            Type = "OuterException",
            Inner = new Error
            {
                Message = "Inner exception",
                Type = "InnerException"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Outer exception", deserialized.Message);
        Assert.NotNull(deserialized.Inner);
        Assert.Equal("Inner exception", deserialized.Inner.Message);
    }

    [Fact]
    public void Deserialize_ErrorWithDeeplyNestedInner_PreservesFullHierarchy()
    {
        // Arrange
        var original = new Error
        {
            Message = "Outer exception",
            Type = "OuterException",
            Inner = new Error
            {
                Message = "Middle exception",
                Type = "MiddleException",
                Inner = new Error
                {
                    Message = "Root cause",
                    Type = "RootException"
                }
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Outer exception", deserialized.Message);
        Assert.NotNull(deserialized.Inner);
        Assert.Equal("Middle exception", deserialized.Inner.Message);
        Assert.NotNull(deserialized.Inner.Inner);
        Assert.Equal("Root cause", deserialized.Inner.Inner.Message);
    }

    [Fact]
    public void Deserialize_ErrorWithDataDictionary_PreservesCustomData()
    {
        // Arrange
        var original = new Error
        {
            Message = "Test exception",
            Type = "System.Exception",
            Data = new DataDictionary
            {
                ["custom_key"] = "custom_value",
                ["error_code"] = 12345
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Data);
        Assert.Equal("custom_value", deserialized.Data["custom_key"]);
    }

    [Fact]
    public void Deserialize_ErrorWithModules_PreservesModuleInfo()
    {
        // Arrange
        var original = new Error
        {
            Message = "Test exception",
            Type = "System.Exception",
            Modules =
            [
                new Module
                {
                    Name = "MyApp.dll",
                    Version = "1.0.0"
                }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Modules);
        Assert.Single(deserialized.Modules);
        Assert.Equal("MyApp.dll", deserialized.Modules[0].Name);
        Assert.Equal("1.0.0", deserialized.Modules[0].Version);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"External error","type":"System.ArgumentException","code":"ARG001","data":{"source":"external"},"stack_trace":[{"name":"Validate","declaring_namespace":"App","declaring_type":"Validator","line_number":10}]}""";

        // Act
        var error = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("External error", error.Message);
        Assert.Equal("System.ArgumentException", error.Type);
        Assert.Equal("ARG001", error.Code);
        Assert.NotNull(error.StackTrace);
        Assert.Single(error.StackTrace);
    }

    [Fact]
    public void Deserialize_ErrorWithSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        var original = new Error
        {
            Message = "Error: \"file not found\" at C:\\Users\\test\\file.txt",
            Type = "System.IO.FileNotFoundException"
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<Error>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Error: \"file not found\" at C:\\Users\\test\\file.txt", deserialized.Message);
    }
}
