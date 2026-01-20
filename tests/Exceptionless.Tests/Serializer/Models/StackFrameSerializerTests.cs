using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests StackFrame serialization through ITextSerializer.
/// Validates round-trip serialization and snake_case property naming.
/// </summary>
public class StackFrameSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public StackFrameSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_StackFrame_UsesSnakeCasePropertyNames()
    {
        // Arrange
        var frame = new StackFrame
        {
            Name = "ProcessEventAsync",
            DeclaringNamespace = "Exceptionless.Core.Pipeline",
            DeclaringType = "EventPipeline",
            FileName = "EventPipeline.cs",
            LineNumber = 142,
            Column = 25
        };

        // Act
        string? json = _serializer.SerializeToString(frame);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert - Verify roundtrip preserves all properties
        Assert.NotNull(deserialized);
        Assert.Equal("ProcessEventAsync", deserialized.Name);
        Assert.Equal("Exceptionless.Core.Pipeline", deserialized.DeclaringNamespace);
        Assert.Equal("EventPipeline", deserialized.DeclaringType);
        Assert.Equal("EventPipeline.cs", deserialized.FileName);
        Assert.Equal(142, deserialized.LineNumber);
        Assert.Equal(25, deserialized.Column);
    }


    [Fact]
    public void Deserialize_CompleteStackFrame_PreservesAllProperties()
    {
        // Arrange
        var original = new StackFrame
        {
            Name = "InvokeAsync",
            DeclaringNamespace = "Microsoft.AspNetCore.Mvc",
            DeclaringType = "ControllerBase",
            FileName = "ControllerBase.cs",
            LineNumber = 500,
            Column = 10
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("InvokeAsync", deserialized.Name);
        Assert.Equal("Microsoft.AspNetCore.Mvc", deserialized.DeclaringNamespace);
        Assert.Equal("ControllerBase", deserialized.DeclaringType);
        Assert.Equal("ControllerBase.cs", deserialized.FileName);
        Assert.Equal(500, deserialized.LineNumber);
        Assert.Equal(10, deserialized.Column);
    }

    [Fact]
    public void Deserialize_StackFrameWithGenericArguments_PreservesGenericTypes()
    {
        // Arrange
        var original = new StackFrame
        {
            Name = "GetAsync",
            DeclaringNamespace = "MyApp.Repositories",
            DeclaringType = "Repository",
            GenericArguments = ["TEntity", "TKey"]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.GenericArguments);
        Assert.Equal(2, deserialized.GenericArguments.Count);
        Assert.Contains("TEntity", deserialized.GenericArguments);
        Assert.Contains("TKey", deserialized.GenericArguments);
    }

    [Fact]
    public void Deserialize_StackFrameWithParameters_PreservesParameters()
    {
        // Arrange
        var original = new StackFrame
        {
            Name = "CreateUser",
            DeclaringNamespace = "MyApp.Services",
            DeclaringType = "UserService",
            Parameters =
            [
                new Parameter { Name = "name", Type = "String" },
                new Parameter { Name = "email", Type = "String" },
                new Parameter { Name = "age", Type = "Int32" }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Parameters);
        Assert.Equal(3, deserialized.Parameters.Count);
        Assert.Equal("name", deserialized.Parameters[0].Name);
        Assert.Equal("String", deserialized.Parameters[0].Type);
    }

    [Fact]
    public void Deserialize_StackFrameWithData_PreservesCustomData()
    {
        // Arrange
        var original = new StackFrame
        {
            Name = "Process",
            Data = new DataDictionary
            {
                ["is_async"] = true,
                ["timing_ms"] = 150
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Data);
        Assert.True(deserialized.Data.ContainsKey("is_async"));
        Assert.True(deserialized.Data.ContainsKey("timing_ms"));
    }


    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"file_name":"Handler.cs","name":"HandleAsync","declaring_namespace":"App.Handlers","declaring_type":"EventHandler","line_number":25,"column":8}""";

        // Act
        var frame = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(frame);
        Assert.Equal("HandleAsync", frame.Name);
        Assert.Equal("App.Handlers", frame.DeclaringNamespace);
        Assert.Equal("Handler.cs", frame.FileName);
        Assert.Equal(25, frame.LineNumber);
    }


    [Fact]
    public void Deserialize_MinimalStackFrame_PreservesName()
    {
        // Arrange
        var original = new StackFrame { Name = "Execute" };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Execute", deserialized.Name);
    }

    [Fact]
    public void Deserialize_StackFrameWithZeroLineNumber_PreservesZero()
    {
        // Arrange
        var original = new StackFrame
        {
            Name = "DynamicMethod",
            LineNumber = 0
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<StackFrame>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("DynamicMethod", deserialized.Name);
        Assert.Equal(0, deserialized.LineNumber);
    }
}
