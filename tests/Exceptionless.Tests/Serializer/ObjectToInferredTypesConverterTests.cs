using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests for ObjectToInferredTypesConverter.
/// Validates that object-typed properties are correctly deserialized to native .NET types
/// instead of JsonElement, enabling proper GetValue behavior.
/// </summary>
public class ObjectToInferredTypesConverterTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ObjectToInferredTypesConverterTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Read_TrueBoolean_ReturnsNativeBool()
    {
        // Arrange
        /* language=json */
        const string json = """{"value": true}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("value"));
        Assert.IsType<bool>(result["value"]);
        Assert.True((bool)result["value"]!);
    }

    [Fact]
    public void Read_FalseBoolean_ReturnsNativeBool()
    {
        // Arrange
        /* language=json */
        const string json = """{"value": false}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<bool>(result["value"]);
        Assert.False((bool)result["value"]!);
    }

    [Fact]
    public void Read_IntegerNumber_ReturnsLong()
    {
        // Arrange
        /* language=json */
        const string json = """{"count": 42}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<long>(result["count"]);
        Assert.Equal(42L, result["count"]);
    }

    [Fact]
    public void Read_LargeInteger_ReturnsLong()
    {
        // Arrange
        /* language=json */
        const string json = """{"bigNumber": 9223372036854775807}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<long>(result["bigNumber"]);
        Assert.Equal(Int64.MaxValue, result["bigNumber"]);
    }

    [Fact]
    public void Read_NegativeInteger_ReturnsLong()
    {
        // Arrange
        /* language=json */
        const string json = """{"negative": -12345}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<long>(result["negative"]);
        Assert.Equal(-12345L, result["negative"]);
    }

    [Fact]
    public void Read_DecimalNumber_ReturnsDouble()
    {
        // Arrange
        /* language=json */
        const string json = """{"price": 99.95}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<double>(result["price"]);
        Assert.Equal(99.95, result["price"]);
    }

    [Fact]
    public void Read_ScientificNotation_ReturnsDouble()
    {
        // Arrange
        /* language=json */
        const string json = """{"value": 1.23e10}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<double>(result["value"]);
        Assert.Equal(1.23e10, result["value"]);
    }

    [Fact]
    public void Read_PlainString_ReturnsString()
    {
        // Arrange
        /* language=json */
        const string json = """{"name": "test value"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result["name"]);
        Assert.Equal("test value", result["name"]);
    }

    [Fact]
    public void Read_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        /* language=json */
        const string json = """{"empty": ""}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result["empty"]);
        Assert.Equal(String.Empty, result["empty"]);
    }

    [Fact]
    public void Read_NullValue_ReturnsNull()
    {
        // Arrange
        /* language=json */
        const string json = """{"nothing": null}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("nothing"));
        Assert.Null(result["nothing"]);
    }

    [Fact]
    public void Read_Iso8601DateTime_ReturnsDateTimeOffset()
    {
        // Arrange
        /* language=json */
        const string json = """{"timestamp": "2024-01-15T12:30:45.123Z"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DateTimeOffset>(result["timestamp"]);
        var dateTime = (DateTimeOffset)result["timestamp"]!;
        Assert.Equal(2024, dateTime.Year);
        Assert.Equal(1, dateTime.Month);
        Assert.Equal(15, dateTime.Day);
        Assert.Equal(12, dateTime.Hour);
        Assert.Equal(30, dateTime.Minute);
        Assert.Equal(45, dateTime.Second);
    }

    [Fact]
    public void Read_Iso8601WithOffset_ReturnsDateTimeOffsetWithOffset()
    {
        // Arrange
        /* language=json */
        const string json = """{"timestamp": "2024-01-15T12:30:45+05:30"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DateTimeOffset>(result["timestamp"]);
        var dateTime = (DateTimeOffset)result["timestamp"]!;
        Assert.Equal(TimeSpan.FromHours(5.5), dateTime.Offset);
    }

    [Fact]
    public void Read_DateOnly_ReturnsDateTimeOffset()
    {
        // Arrange
        /* language=json */
        const string json = """{"date": "2024-01-15"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DateTimeOffset>(result["date"]);
    }

    [Fact]
    public void Read_NonDateString_ReturnsString()
    {
        // Arrange
        /* language=json */
        const string json = """{"notADate": "hello world"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result["notADate"]);
        Assert.Equal("hello world", result["notADate"]);
    }

    [Fact]
    public void Read_NestedObject_ReturnsDictionary()
    {
        // Arrange
        /* language=json */
        const string json = """{"user": {"identity": "test@example.com", "name": "Test User"}}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, object?>>(result["user"]);

        var user = (Dictionary<string, object?>)result["user"]!;
        Assert.Equal("test@example.com", user["identity"]);
        Assert.Equal("Test User", user["name"]);
    }

    [Fact]
    public void Read_DeeplyNestedObject_ReturnsDictionaryHierarchy()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "level1": {
                "level2": {
                    "level3": {
                        "value": "deep"
                    }
                }
            }
        }
        """;

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var level1 = Assert.IsType<Dictionary<string, object?>>(result["level1"]);
        var level2 = Assert.IsType<Dictionary<string, object?>>(level1["level2"]);
        var level3 = Assert.IsType<Dictionary<string, object?>>(level2["level3"]);
        Assert.Equal("deep", level3["value"]);
    }

    [Fact]
    public void Read_EmptyObject_ReturnsEmptyDictionary()
    {
        // Arrange
        /* language=json */
        const string json = """{"empty": {}}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var empty = Assert.IsType<Dictionary<string, object?>>(result["empty"]);
        Assert.Empty(empty);
    }

    [Fact]
    public void Read_ObjectWithMixedTypes_ReturnsCorrectTypes()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "data": {
                "count": 42,
                "name": "test",
                "active": true,
                "nullable": null,
                "timestamp": "2024-01-15T12:00:00Z"
            }
        }
        """;

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var data = Assert.IsType<Dictionary<string, object?>>(result["data"]);

        Assert.IsType<long>(data["count"]);
        Assert.IsType<string>(data["name"]);
        Assert.IsType<bool>(data["active"]);
        Assert.Null(data["nullable"]);
        Assert.IsType<DateTimeOffset>(data["timestamp"]);
    }

    [Fact]
    public void Read_ArrayOfStrings_ReturnsList()
    {
        // Arrange
        /* language=json */
        const string json = """{"tags": ["one", "two", "three"]}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var tags = Assert.IsType<List<object?>>(result["tags"]);
        Assert.Equal(3, tags.Count);
        Assert.Equal("one", tags[0]);
        Assert.Equal("two", tags[1]);
        Assert.Equal("three", tags[2]);
    }

    [Fact]
    public void Read_ArrayOfNumbers_ReturnsListOfLongs()
    {
        // Arrange
        /* language=json */
        const string json = """{"numbers": [1, 2, 3, 4, 5]}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var numbers = Assert.IsType<List<object?>>(result["numbers"]);
        Assert.All(numbers, n => Assert.IsType<long>(n));
        Assert.Equal(new List<object?> { 1L, 2L, 3L, 4L, 5L }, numbers);
    }

    [Fact]
    public void Read_ArrayOfMixedTypes_ReturnsListWithCorrectTypes()
    {
        // Arrange
        /* language=json */
        const string json = """{"mixed": ["string", 42, true, null, 3.14]}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var mixed = Assert.IsType<List<object?>>(result["mixed"]);
        Assert.Equal(5, mixed.Count);
        Assert.IsType<string>(mixed[0]);
        Assert.IsType<long>(mixed[1]);
        Assert.IsType<bool>(mixed[2]);
        Assert.Null(mixed[3]);
        Assert.IsType<double>(mixed[4]);
    }

    [Fact]
    public void Read_ArrayOfObjects_ReturnsListOfDictionaries()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "items": [
                {"name": "first", "value": 1},
                {"name": "second", "value": 2}
            ]
        }
        """;

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var items = Assert.IsType<List<object?>>(result["items"]);
        Assert.Equal(2, items.Count);

        var first = Assert.IsType<Dictionary<string, object?>>(items[0]);
        Assert.Equal("first", first["name"]);
        Assert.Equal(1L, first["value"]);

        var second = Assert.IsType<Dictionary<string, object?>>(items[1]);
        Assert.Equal("second", second["name"]);
        Assert.Equal(2L, second["value"]);
    }

    [Fact]
    public void Read_NestedArrays_ReturnsNestedLists()
    {
        // Arrange
        /* language=json */
        const string json = """{"matrix": [[1, 2], [3, 4], [5, 6]]}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var matrix = Assert.IsType<List<object?>>(result["matrix"]);
        Assert.Equal(3, matrix.Count);

        var row1 = Assert.IsType<List<object?>>(matrix[0]);
        Assert.Equal(new List<object?> { 1L, 2L }, row1);
    }

    [Fact]
    public void Read_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        /* language=json */
        const string json = """{"empty": []}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        var empty = Assert.IsType<List<object?>>(result["empty"]);
        Assert.Empty(empty);
    }

    [Fact]
    public void Read_ObjectWithVariedCasing_SupportsCaseInsensitiveAccess()
    {
        // Arrange
        /* language=json */
        const string json = """{"data": {"UserName": "test", "user_email": "test@example.com", "userId": 123}}""";

        // Act
        var wrapper = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(wrapper);
        var result = Assert.IsType<Dictionary<string, object?>>(wrapper["data"]);

        // Nested dictionaries created by our converter ARE case-insensitive
        Assert.Equal("test", result["username"]);
        Assert.Equal("test@example.com", result["USER_EMAIL"]);
        Assert.Equal(123L, result["USERID"]);
    }

    [Fact]
    public void Write_DictionaryWithPrimitives_SerializesCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["name"] = "test",
            ["count"] = 42L,
            ["active"] = true,
            ["nothing"] = null
        };

        // Act
        string? json = _serializer.SerializeToString(data);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"name\":\"test\"", json);
        Assert.Contains("\"count\":42", json);
        Assert.Contains("\"active\":true", json);
        Assert.Contains("\"nothing\":null", json);
    }

    [Fact]
    public void Write_NestedDictionary_SerializesCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["outer"] = new Dictionary<string, object?>
            {
                ["inner"] = "value"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(data);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"outer\":{\"inner\":\"value\"}", json);
    }

    [Fact]
    public void Write_ListOfValues_SerializesCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "a", 1L, true }
        };

        // Act
        string? json = _serializer.SerializeToString(data);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"items\":[\"a\",1,true]", json);
    }

    [Fact]
    public void Deserialize_ComplexStructureAfterRoundTrip_PreservesData()
    {
        // Arrange
        var original = new Dictionary<string, object?>
        {
            ["name"] = "test",
            ["count"] = 42L,
            ["active"] = true,
            ["price"] = 99.95,
            ["tags"] = new List<object?> { "one", "two" },
            ["nested"] = new Dictionary<string, object?>
            {
                ["inner"] = "value",
                ["number"] = 123L
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var roundTripped = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal("test", roundTripped["name"]);
        Assert.Equal(42L, roundTripped["count"]);
        Assert.True((bool)roundTripped["active"]!);
        Assert.Equal(99.95, roundTripped["price"]);

        var tags = Assert.IsType<List<object?>>(roundTripped["tags"]);
        Assert.Equal(2, tags.Count);

        var nested = Assert.IsType<Dictionary<string, object?>>(roundTripped["nested"]);
        Assert.Equal("value", nested["inner"]);
        Assert.Equal(123L, nested["number"]);
    }

    [Fact]
    public void Read_SpecialCharactersInString_PreservesCharacters()
    {
        // Arrange
        /* language=json */
        const string json = """{"text": "hello\nworld\ttab\"quote"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hello\nworld\ttab\"quote", result["text"]);
    }

    [Fact]
    public void Read_UnicodeString_PreservesUnicode()
    {
        // Arrange
        /* language=json */
        const string json = """{"text": "Hello 世界 🌍"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello 世界 🌍", result["text"]);
    }

    [Fact]
    public void Read_VeryLongString_PreservesContent()
    {
        // Arrange
        string longString = new('x', 10000);
        string json = $$"""{"long": "{{longString}}"}""";

        // Act
        var result = _serializer.Deserialize<Dictionary<string, object?>>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longString, result["long"]);
    }

    [Fact]
    public void Read_NumberAtInt64Boundary_HandlesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json1 = """{"value": 9223372036854775807}""";
        /* language=json */
        const string json2 = """{"value": 9223372036854775808}""";

        // Act
        var result1 = _serializer.Deserialize<Dictionary<string, object?>>(json1);
        var result2 = _serializer.Deserialize<Dictionary<string, object?>>(json2);

        // Assert - Number that fits in long
        Assert.NotNull(result1);
        Assert.IsType<long>(result1["value"]);
        Assert.Equal(Int64.MaxValue, result1["value"]);

        // Assert - Number exceeding long.MaxValue becomes double
        Assert.NotNull(result2);
        Assert.IsType<double>(result2["value"]);
    }
}
