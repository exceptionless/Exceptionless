using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests ParameterCollection serialization through ITextSerializer.
/// ParameterCollection extends Collection&lt;Parameter&gt; directly.
/// </summary>
public class ParameterCollectionSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ParameterCollectionSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesAllParameters()
    {
        // Arrange
        var collection = new ParameterCollection
        {
            new()
            {
                Name = "context",
                Type = "PipelineContext",
                TypeNamespace = "Exceptionless.Core.Pipeline",
                Data = new DataDictionary { ["IsValid"] = true },
                GenericArguments = new GenericArguments { "EventContext" }
            },
            new()
            {
                Name = "cancellationToken",
                Type = "CancellationToken",
                TypeNamespace = "System.Threading"
            }
        };

        // Act
        string json = _serializer.SerializeToString(collection);
        var deserialized = _serializer.Deserialize<ParameterCollection>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);

        Assert.Equal("context", deserialized[0].Name);
        Assert.Equal("PipelineContext", deserialized[0].Type);
        Assert.Equal("Exceptionless.Core.Pipeline", deserialized[0].TypeNamespace);
        Assert.NotNull(deserialized[0].Data);
        Assert.NotNull(deserialized[0].GenericArguments);
        Assert.Single(deserialized[0].GenericArguments!);
        Assert.Equal("EventContext", deserialized[0].GenericArguments![0]);

        Assert.Equal("cancellationToken", deserialized[1].Name);
        Assert.Equal("CancellationToken", deserialized[1].Type);
        Assert.Equal("System.Threading", deserialized[1].TypeNamespace);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyCollection()
    {
        // Arrange
        /* language=json */
        const string json = """[]""";

        // Act
        var collection = _serializer.Deserialize<ParameterCollection>(json);

        // Assert
        Assert.NotNull(collection);
        Assert.Empty(collection);
    }

    [Fact]
    public void SerializeToString_UsesSnakeCasePropertyNames()
    {
        // Arrange
        var collection = new ParameterCollection
        {
            new()
            {
                Name = "request",
                Type = "HttpRequest",
                TypeNamespace = "Microsoft.AspNetCore.Http",
                GenericArguments = new GenericArguments { "string", "int" }
            }
        };

        // Act
        string json = _serializer.SerializeToString(collection);

        // Assert
        Assert.Contains("type_namespace", json);
        Assert.Contains("generic_arguments", json);
        Assert.DoesNotContain("TypeNamespace", json);
        Assert.DoesNotContain("GenericArguments", json);
    }
}
