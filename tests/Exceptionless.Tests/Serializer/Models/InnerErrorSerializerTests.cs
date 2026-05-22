using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class InnerErrorSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public InnerErrorSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"File not found","type":"System.IO.FileNotFoundException","code":"IO_404","target_method":{"name":"ReadFile","declaring_type":"FileService","declaring_namespace":"MyApp.IO"},"stack_trace":[{"declaring_namespace":"MyApp.IO","declaring_type":"FileService","name":"ReadFile","line":42}]}""";

        // Act
        var result = _serializer.Deserialize<InnerError>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("File not found", result.Message);
        Assert.Equal("System.IO.FileNotFoundException", result.Type);
        Assert.Equal("IO_404", result.Code);
        Assert.NotNull(result.TargetMethod);
        Assert.Equal("ReadFile", result.TargetMethod.Name);
        Assert.NotNull(result.StackTrace);
        Assert.Single(result.StackTrace);
    }



    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        // Arrange
        var error = new InnerError
        {
            Message = "Object reference not set to an instance of an object.",
            Type = "System.NullReferenceException",
            Code = "NRE001",
            Data = new DataDictionary { ["help_link"] = "https://docs.example.com/nre" },
            StackTrace =
            [
                new StackFrame
                {
                    DeclaringNamespace = "MyApp",
                    DeclaringType = "Service",
                    Name = "Process",
                    Data = new DataDictionary { ["il_offset"] = 42 }
                }
            ],
            TargetMethod = new Method
            {
                Name = "Process",
                DeclaringType = "Service",
                DeclaringNamespace = "MyApp"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var result = _serializer.Deserialize<InnerError>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Object reference not set to an instance of an object.", result.Message);
        Assert.Equal("System.NullReferenceException", result.Type);
        Assert.Equal("NRE001", result.Code);
        Assert.NotNull(result.StackTrace);
        Assert.Single(result.StackTrace);
        Assert.Equal("Process", result.StackTrace[0].Name);
        Assert.NotNull(result.TargetMethod);
        Assert.Equal("Process", result.TargetMethod.Name);
    }

    [Fact]
    public void RoundTrip_WithNestedInnerErrors_PreservesDepth()
    {
        // Arrange
        var error = new InnerError
        {
            Message = "Outer error",
            Type = "System.AggregateException",
            Inner = new InnerError
            {
                Message = "Middle error",
                Type = "System.InvalidOperationException",
                Inner = new InnerError
                {
                    Message = "Root cause",
                    Type = "System.ArgumentNullException",
                    Code = "ARG_NULL"
                }
            }
        };

        // Act
        string? json = _serializer.SerializeToString(error);
        var result = _serializer.Deserialize<InnerError>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Outer error", result.Message);
        Assert.NotNull(result.Inner);
        Assert.Equal("Middle error", result.Inner.Message);
        Assert.NotNull(result.Inner.Inner);
        Assert.Equal("Root cause", result.Inner.Inner.Message);
        Assert.Equal("ARG_NULL", result.Inner.Inner.Code);
        Assert.Null(result.Inner.Inner.Inner);
    }
}
