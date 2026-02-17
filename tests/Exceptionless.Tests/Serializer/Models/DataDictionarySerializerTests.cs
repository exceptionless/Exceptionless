using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Newtonsoft.Json.Linq;
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
    /// Reproduces production bug where JObject/JArray values in DataDictionary
    /// (stored by Newtonsoft-based DataObjectConverter when reading from Elasticsearch)
    /// serialize as nested empty arrays instead of proper JSON when written by STJ.
    /// </summary>
    [Fact]
    public void Serialize_JObjectValue_WritesCorrectJson()
    {
        // Arrange — simulate Elasticsearch read path storing JObject in DataDictionary
        var jObject = JObject.Parse("""
            {
                "docsSecondari": [
                    { "tipo": "CI", "numero": "AB123" },
                    { "tipo": "PP", "numero": "CD456" }
                ],
                "docPrimario": { "tipo": "DL", "numero": "XY789" },
                "numeroDocumentiSecondari": 2,
                "AlreadyImported": true
            }
            """);

        var data = new DataDictionary { ["TestUfficialeVO"] = jObject };

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
    /// Verifies JArray values in DataDictionary serialize correctly.
    /// </summary>
    [Fact]
    public void Serialize_JArrayValue_WritesCorrectJson()
    {
        // Arrange — simulate Elasticsearch storing JArray in DataDictionary
        var jArray = JArray.Parse("""["tag1", "tag2", "tag3"]""");
        var data = new DataDictionary { ["Tags"] = jArray };

        // Act
        string json = _serializer.SerializeToString(data);

        // Assert
        Assert.Contains("tag1", json);
        Assert.Contains("tag2", json);
        Assert.Contains("tag3", json);
        Assert.DoesNotContain("[[]]", json);
    }

    /// <summary>
    /// Verifies deeply nested JObject structures serialize correctly,
    /// matching the exact production data pattern that was broken.
    /// </summary>
    [Fact]
    public void Serialize_DeeplyNestedJObject_PreservesStructure()
    {
        // Arrange — nested structure matching production data shape
        var jObject = JObject.Parse("""
            {
                "items": [
                    {
                        "name": "item1",
                        "children": [
                            { "id": 1, "value": "a" },
                            { "id": 2, "value": "b" }
                        ]
                    }
                ],
                "count": 1
            }
            """);

        var data = new DataDictionary { ["NestedData"] = jObject };

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
