using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests the ITextSerializer interface contract.
/// Validates that all serialization methods work correctly and consistently.
/// These tests ensure the serializer can be swapped without breaking functionality.
/// </summary>
public class ITextSerializerContractTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ITextSerializerContractTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    private sealed record SimpleModel(string Name, int Value);

    [Fact]
    public void SerializeToString_WithSimpleObject_ProducesExpectedJson()
    {
        // Arrange
        var model = new SimpleModel("test", 42);

        /* language=json */
        const string expectedJson = """{"name":"test","value":42}""";

        // Act
        string? json = _serializer.SerializeToString(model);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void SerializeToString_WithNullObject_ReturnsNull()
    {
        // Arrange
        SimpleModel? model = null;

        // Act
        string? json = _serializer.SerializeToString(model);

        // Assert
        Assert.Null(json);
    }

    [Fact]
    public void SerializeToString_WithEmptyStringProperty_SerializesEmptyString()
    {
        // Arrange
        var model = new SimpleModel("", 0);

        /* language=json */
        const string expectedJson = """{"name":"","value":0}""";

        // Act
        string? json = _serializer.SerializeToString(model);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void SerializeToString_WithSpecialCharacters_EscapesCorrectly()
    {
        // Arrange
        var model = new SimpleModel("line1\nline2\ttab\"quote", 0);

        /* language=json */
        const string expectedJson = """{"name":"line1\nline2\ttab\"quote","value":0}""";

        // Act
        string? json = _serializer.SerializeToString(model);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void SerializeToBytes_WithSimpleObject_ProducesUtf8Bytes()
    {
        // Arrange
        var model = new SimpleModel("test", 42);

        /* language=json */
        const string expectedJson = """{"name":"test","value":42}""";
        byte[] expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedJson);

        // Act
        byte[]? bytes = _serializer.SerializeToBytes(model);

        // Assert
        Assert.Equal(expectedBytes, bytes.ToArray());
    }

    [Fact]
    public void SerializeToBytes_WithUnicodeString_ProducesCorrectUtf8()
    {
        // Arrange
        var model = new SimpleModel("日本語", 1);

        /* language=json */
        const string expectedJson = """{"name":"日本語","value":1}""";
        byte[] expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedJson);

        // Act
        byte[]? bytes = _serializer.SerializeToBytes(model);

        // Assert
        Assert.Equal(expectedBytes, bytes.ToArray());
    }

    [Fact]
    public void Serialize_ToStream_WritesExpectedJson()
    {
        // Arrange
        var model = new SimpleModel("stream", 99);

        /* language=json */
        const string expectedJson = """{"name":"stream","value":99}""";
        using var stream = new MemoryStream();

        // Act
        _serializer.Serialize(model, stream);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        string json = reader.ReadToEnd();
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void Deserialize_WithValidJson_ReturnsPopulatedModel()
    {
        // Arrange
        /* language=json */
        const string json = """{"name":"parsed","value":123}""";

        // Act
        var model = _serializer.Deserialize<SimpleModel>(json);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("parsed", model.Name);
        Assert.Equal(123, model.Value);
    }

    [Fact]
    public void Deserialize_WithNullLiteral_ReturnsNull()
    {
        // Arrange
        /* language=json */
        const string json = "null";

        // Act
        var model = _serializer.Deserialize<SimpleModel>(json);

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public void Deserialize_WithEmptyString_ReturnsNull()
    {
        // Arrange
        string json = String.Empty;

        // Act
        var model = _serializer.Deserialize<SimpleModel>(json);

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public void Deserialize_WithWhitespaceOnlyString_ReturnsNull()
    {
        // Arrange
        const string json = "   ";

        // Act
        var model = _serializer.Deserialize<SimpleModel>(json);

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public void Deserialize_FromStream_ReturnsPopulatedModel()
    {
        // Arrange
        /* language=json */
        const string json = """{"name":"from_stream","value":456}""";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        // Act
        var model = _serializer.Deserialize<SimpleModel>(stream);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("from_stream", model.Name);
        Assert.Equal(456, model.Value);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ThrowsException()
    {
        // Arrange
        const string invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => _serializer.Deserialize<SimpleModel>(invalidJson));
    }

    [Fact]
    public void Deserialize_WithSpecialCharacters_PreservesData()
    {
        // Arrange
        /* language=json */
        const string json = """{"name":"line1\nline2\ttab\"quote\\backslash","value":0}""";

        // Act
        var model = _serializer.Deserialize<SimpleModel>(json);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("line1\nline2\ttab\"quote\\backslash", model.Name);
    }

    [Fact]
    public void SerializeToString_ThenDeserialize_PreservesStringData()
    {
        // Arrange
        var original = new SimpleModel("round-trip", 789);

        /* language=json */
        const string expectedJson = """{"name":"round-trip","value":789}""";

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<SimpleModel>(json);

        // Assert
        Assert.Equal(expectedJson, json);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Value, deserialized.Value);
    }

    [Fact]
    public void SerializeToBytes_ThenDeserialize_PreservesBytesData()
    {
        // Arrange
        var original = new SimpleModel("bytes-trip", 321);

        // Act
        byte[]? bytes = _serializer.SerializeToBytes(original);
        using var stream = new MemoryStream(bytes.ToArray());
        var deserialized = _serializer.Deserialize<SimpleModel>(stream);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Value, deserialized.Value);
    }

    [Fact]
    public void Serialize_ThenDeserialize_ThroughStream_PreservesData()
    {
        // Arrange
        var original = new SimpleModel("stream-trip", 654);
        using var stream = new MemoryStream();

        // Act
        _serializer.Serialize(original, stream);
        stream.Position = 0;
        var deserialized = _serializer.Deserialize<SimpleModel>(stream);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Value, deserialized.Value);
    }

    private sealed class ComplexModel
    {
        public string? Id { get; set; }
        public List<string> Tags { get; set; } = [];
        public Dictionary<string, object?> Metadata { get; set; } = [];
        public NestedModel? Nested { get; set; }
    }

    private sealed class NestedModel
    {
        public string? Description { get; set; }
        public int Priority { get; set; }
    }

    [Fact]
    public void SerializeToString_WithComplexObject_ProducesExpectedJson()
    {
        // Arrange
        var model = new ComplexModel
        {
            Id = "complex1",
            Tags = ["tag1", "tag2"],
            Metadata = new Dictionary<string, object?>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            },
            Nested = new NestedModel
            {
                Description = "nested desc",
                Priority = 5
            }
        };

        /* language=json */
        const string expectedJson = """{"id":"complex1","tags":["tag1","tag2"],"metadata":{"key1":"value1","key2":42},"nested":{"description":"nested desc","priority":5}}""";

        // Act
        string? json = _serializer.SerializeToString(model);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void Deserialize_WithComplexJson_ReturnsPopulatedModel()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"complex-rt","tags":["a","b","c"],"metadata":{"count":100,"enabled":true},"nested":{"description":"test","priority":10}}""";

        // Act
        var model = _serializer.Deserialize<ComplexModel>(json);

        // Assert
        Assert.NotNull(model);
        Assert.Equal("complex-rt", model.Id);
        Assert.Equal(3, model.Tags.Count);
        Assert.Contains("a", model.Tags);
        Assert.Contains("b", model.Tags);
        Assert.Contains("c", model.Tags);
        Assert.NotNull(model.Nested);
        Assert.Equal("test", model.Nested.Description);
        Assert.Equal(10, model.Nested.Priority);
    }
}
