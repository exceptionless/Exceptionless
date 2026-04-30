using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests DataDictionary serialization through ITextSerializer.
/// DataDictionary extends Dictionary&lt;string, object?&gt; directly, so STJ
/// handles it natively. These tests guard against regressions.
/// </summary>
public class DataDictionarySerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public DataDictionarySerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesEntries()
    {
        // Arrange
        var data = new DataDictionary
        {
            { "StringKey", "value" },
            { "IntKey", 42 },
            { "BoolKey", true }
        };

        // Act
        string json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        Assert.Equal("value", deserialized.GetString("StringKey"));
    }

    [Fact]
    public void Deserialize_EmptyDictionary_ReturnsEmptyData()
    {
        // Arrange
        /* language=json */
        const string json = """{}""";

        // Act
        var data = _serializer.Deserialize<DataDictionary>(json);

        // Assert
        Assert.NotNull(data);
        Assert.Empty(data);
    }

    /// <summary>
    /// Verifies Dictionary values in DataDictionary (from ObjectToInferredTypesConverter
    /// when reading from Elasticsearch) serialize correctly to JSON.
    /// </summary>
    [Fact]
    public void Serialize_DictionaryValue_WritesCorrectJson()
    {
        // Arrange — simulate Elasticsearch read path storing Dictionary in DataDictionary
        var dict = new Dictionary<string, object?>
        {
            ["docsSecondari"] = new List<object?>
            {
                new Dictionary<string, object?> { ["tipo"] = "CI", ["numero"] = "AB123" },
                new Dictionary<string, object?> { ["tipo"] = "PP", ["numero"] = "CD456" }
            },
            ["docPrimario"] = new Dictionary<string, object?> { ["tipo"] = "DL", ["numero"] = "XY789" },
            ["numeroDocumentiSecondari"] = 2,
            ["AlreadyImported"] = true
        };

        var data = new DataDictionary { ["TestUfficialeVO"] = dict };

        // Act
        string json = _serializer.SerializeToString(data);

        // Assert — must contain actual property values, not nested empty arrays
        Assert.Contains("docsSecondari", json);
        Assert.Contains("CI", json);
        Assert.Contains("AB123", json);
        Assert.Contains("docPrimario", json);
        Assert.Contains("XY789", json);
        Assert.DoesNotContain("[[[]]]", json);
    }

    /// <summary>
    /// Verifies List values in DataDictionary serialize correctly.
    /// </summary>
    [Fact]
    public void Serialize_ListValue_WritesCorrectJson()
    {
        // Arrange — simulate Elasticsearch storing List in DataDictionary
        var list = new List<object?> { "tag1", "tag2", "tag3" };
        var data = new DataDictionary { ["Tags"] = list };

        // Act
        string json = _serializer.SerializeToString(data);

        // Assert
        Assert.Contains("tag1", json);
        Assert.Contains("tag2", json);
        Assert.Contains("tag3", json);
        Assert.DoesNotContain("[[]]", json);
    }

    /// <summary>
    /// Verifies deeply nested Dictionary structures serialize correctly,
    /// matching the exact production data pattern.
    /// </summary>
    [Fact]
    public void Serialize_DeeplyNestedDictionary_PreservesStructure()
    {
        // Arrange — nested structure matching production data shape
        var dict = new Dictionary<string, object?>
        {
            ["items"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["name"] = "item1",
                    ["children"] = new List<object?>
                    {
                        new Dictionary<string, object?> { ["id"] = 1, ["value"] = "a" },
                        new Dictionary<string, object?> { ["id"] = 2, ["value"] = "b" }
                    }
                }
            },
            ["count"] = 1
        };

        var data = new DataDictionary { ["NestedData"] = dict };

        // Act
        string json = _serializer.SerializeToString(data);
        var deserialized = _serializer.Deserialize<DataDictionary>(json);

        // Assert — roundtrip preserves structure
        Assert.NotNull(deserialized);
        Assert.Contains("item1", json);
        Assert.Contains("\"id\"", json);
        Assert.Contains("\"value\"", json);
    }
}
