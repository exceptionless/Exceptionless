using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests StackFrameCollection serialization through ITextSerializer.
/// StackFrameCollection extends Collection&lt;StackFrame&gt; directly.
/// Individual StackFrame serialization is covered in StackFrameSerializerTests.
/// </summary>
public class StackFrameCollectionSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public StackFrameCollectionSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesAllFrames()
    {
        // Arrange
        var collection = new StackFrameCollection
        {
            new()
            {
                Name = "ProcessEventAsync",
                DeclaringNamespace = "Exceptionless.Core.Pipeline",
                DeclaringType = "EventPipeline",
                FileName = "EventPipeline.cs",
                LineNumber = 142,
                Column = 25
            },
            new()
            {
                Name = "ExecuteAsync",
                DeclaringNamespace = "Exceptionless.Core.Jobs",
                DeclaringType = "EventPostsJob",
                FileName = "EventPostsJob.cs",
                LineNumber = 88
            }
        };

        // Act
        string json = _serializer.SerializeToString(collection);
        var deserialized = _serializer.Deserialize<StackFrameCollection>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("ProcessEventAsync", deserialized[0].Name);
        Assert.Equal("EventPipeline.cs", deserialized[0].FileName);
        Assert.Equal(142, deserialized[0].LineNumber);
        Assert.Equal(25, deserialized[0].Column);
        Assert.Equal("ExecuteAsync", deserialized[1].Name);
        Assert.Equal("EventPostsJob.cs", deserialized[1].FileName);
        Assert.Equal(88, deserialized[1].LineNumber);
        Assert.Null(deserialized[1].Column);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyCollection()
    {
        // Arrange
        /* language=json */
        const string json = """[]""";

        // Act
        var collection = _serializer.Deserialize<StackFrameCollection>(json);

        // Assert
        Assert.NotNull(collection);
        Assert.Empty(collection);
    }

    [Fact]
    public void SerializeToString_UsesSnakeCasePropertyNames()
    {
        // Arrange
        var collection = new StackFrameCollection
        {
            new()
            {
                Name = "Main",
                DeclaringType = "Program",
                FileName = "Program.cs",
                LineNumber = 10
            }
        };

        // Act
        string json = _serializer.SerializeToString(collection);

        // Assert
        Assert.Contains("declaring_type", json);
        Assert.Contains("file_name", json);
        Assert.Contains("line_number", json);
        Assert.DoesNotContain("DeclaringType", json);
        Assert.DoesNotContain("FileName", json);
        Assert.DoesNotContain("LineNumber", json);
    }
}
