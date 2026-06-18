using System.Collections.ObjectModel;
using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests verifying that events submitted with PascalCase and camelCase property names
/// are deserialized and processed correctly — matching behavior from main branch (Newtonsoft).
/// These tests reproduce issues identified in the serialization audit diff.
/// </summary>
public class CasingCompatibilityTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public CasingCompatibilityTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
        _jsonOptions = GetService<JsonSerializerOptions>();
    }

    [Theory]
    [InlineData("""{"type":"error"}""", "error")]
    [InlineData("""{"Type":"error"}""", "error")]
    [InlineData("""{"TYPE":"error"}""", "error")]
    public void Deserialize_TypeProperty_MatchesAllCasings(string json, string expected)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal(expected, ev.Type);
    }

    [Theory]
    [InlineData("""{"message":"hello"}""", "hello")]
    [InlineData("""{"Message":"hello"}""", "hello")]
    [InlineData("""{"MESSAGE":"hello"}""", "hello")]
    public void Deserialize_MessageProperty_MatchesAllCasings(string json, string expected)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal(expected, ev.Message);
    }

    [Theory]
    [InlineData("""{"value":42.5}""", 42.5)]
    [InlineData("""{"Value":42.5}""", 42.5)]
    [InlineData("""{"VALUE":42.5}""", 42.5)]
    public void Deserialize_ValueProperty_MatchesAllCasings(string json, decimal expected)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal(expected, ev.Value);
    }

    [Theory]
    [InlineData("""{"count":5}""", 5)]
    [InlineData("""{"Count":5}""", 5)]
    [InlineData("""{"COUNT":5}""", 5)]
    public void Deserialize_CountProperty_MatchesAllCasings(string json, int expected)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal(expected, ev.Count);
    }

    [Theory]
    [InlineData("""{"tags":["a","b"]}""")]
    [InlineData("""{"Tags":["a","b"]}""")]
    [InlineData("""{"TAGS":["a","b"]}""")]
    public void Deserialize_TagsProperty_MatchesAllCasings(string json)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Tags);
        Assert.Equal(2, ev.Tags.Count);
        Assert.Contains("a", ev.Tags);
        Assert.Contains("b", ev.Tags);
    }

    [Theory]
    [InlineData("""{"geo":"40.7,-74.0"}""", "40.7,-74.0")]
    [InlineData("""{"Geo":"40.7,-74.0"}""", "40.7,-74.0")]
    [InlineData("""{"GEO":"40.7,-74.0"}""", "40.7,-74.0")]
    public void Deserialize_GeoProperty_MatchesAllCasings(string json, string expected)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal(expected, ev.Geo);
    }

    [Theory]
    [InlineData("""{"reference_id":"abc"}""", "abc")]
    [InlineData("""{"REFERENCE_ID":"abc"}""", "abc")]
    [InlineData("""{"referenceId":"abc"}""", "abc")]
    [InlineData("""{"ReferenceId":"abc"}""", "abc")]
    public void Deserialize_ReferenceId_MatchesAllCasings(string json, string expected)
    {
        // Arrange

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal(expected, ev.ReferenceId);
    }

    [Fact]
    public void Deserialize_PascalCaseFullEvent_AllPropertiesBound()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "Type": "error",
            "Message": "Test error with PascalCase",
            "Tags": ["audit", "PascalCase"],
            "ReferenceId": "pascal-001",
            "Count": 1,
            "Value": 42.5,
            "Geo": "40.7128,-74.0060",
            "Date": "2026-05-20T12:00:00+00:00"
        }
        """;

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("Test error with PascalCase", ev.Message);
        Assert.Equal("pascal-001", ev.ReferenceId);
        Assert.Equal(1, ev.Count);
        Assert.Equal(42.5m, ev.Value);
        Assert.Equal("40.7128,-74.0060", ev.Geo);
        Assert.NotNull(ev.Tags);
        Assert.Equal(2, ev.Tags.Count);
        Assert.Contains("audit", ev.Tags);
        Assert.Contains("PascalCase", ev.Tags);
    }

    [Fact]
    public void Deserialize_CamelCaseFullEvent_AllPropertiesBound()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "type": "error",
            "message": "Test error with camelCase",
            "tags": ["audit", "camelCase"],
            "referenceId": "camel-001",
            "count": 1,
            "value": 42.5,
            "geo": "40.7128,-74.0060",
            "date": "2026-05-20T12:00:00+00:00"
        }
        """;

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("error", ev.Type);
        Assert.Equal("Test error with camelCase", ev.Message);
        Assert.Equal("camel-001", ev.ReferenceId);
        Assert.Equal(1, ev.Count);
        Assert.Equal(42.5m, ev.Value);
        Assert.Equal("40.7128,-74.0060", ev.Geo);
        Assert.NotNull(ev.Tags);
        Assert.Equal(2, ev.Tags.Count);
    }

    [Fact]
    public void Deserialize_DateOnlyString_PreservedAsString()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "type": "log",
            "data": {
                "date_only": "2026-01-15",
                "not_a_date": "2026-13-45T99:99:99Z",
                "iso_utc": "2026-05-20T12:00:00Z"
            }
        }
        """;

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);

        var dateOnly = ev.Data["date_only"];
        Assert.IsType<string>(dateOnly);
        Assert.Equal("2026-01-15", dateOnly);

        // Invalid date strings should always stay as strings
        var notADate = ev.Data["not_a_date"];
        Assert.IsType<string>(notADate);
        Assert.Equal("2026-13-45T99:99:99Z", notADate);

        // Full ISO dates ARE expected to parse to DateTimeOffset
        var isoUtc = ev.Data["iso_utc"];
        Assert.IsType<DateTimeOffset>(isoUtc);
    }

    [Fact]
    public void Deserialize_NumericZeroVsZeroPointZero_Preserved()
    {
        // Arrange
        /* language=json */
        const string json = """
        {
            "type": "log",
            "data": {
                "zero_int": 0,
                "zero_float": 0.0,
                "one_point_zero": 1.0,
                "one_int": 1
            }
        }
        """;

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);

        // Integer 0 should be int (or long in ES mode)
        var zeroInt = ev.Data["zero_int"];
        Assert.IsType<int>(zeroInt);
        Assert.Equal(0, zeroInt);

        // 0.0 should be decimal (floating-point preserved)
        var zeroFloat = ev.Data["zero_float"];
        Assert.IsType<decimal>(zeroFloat);
        Assert.Equal(0.0m, zeroFloat);

        // 1.0 should be decimal
        var oneFloat = ev.Data["one_point_zero"];
        Assert.IsType<decimal>(oneFloat);
        Assert.Equal(1.0m, oneFloat);

        // 1 should be int
        var oneInt = ev.Data["one_int"];
        Assert.IsType<int>(oneInt);
        Assert.Equal(1, oneInt);
    }

    [Fact]
    public void Serialize_EventValueZero_SerializesAsInteger()
    {
        // Arrange
        var ev = new Event { Type = "log", Value = 0m };

        // Act
        string json = JsonSerializer.Serialize(ev, _jsonOptions);

        // Assert
        var doc = JsonDocument.Parse(json);
        var valueElement = doc.RootElement.GetProperty("value");
        Assert.Equal("0", valueElement.GetRawText());
    }

    [Fact]
    public void Serialize_EmptyTags_OmittedFromOutput()
    {
        // Arrange
        var ev = new Event { Type = "log", Tags = [] };

        // Act
        string json = JsonSerializer.Serialize(ev, _jsonOptions);

        // Assert
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("tags", out _));
    }

    [Fact]
    public void Serialize_EmptyData_OmittedFromOutput()
    {
        // Arrange
        var ev = new Event { Type = "log", Data = new DataDictionary() };

        // Act
        string json = JsonSerializer.Serialize(ev, _jsonOptions);

        // Assert
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("data", out _));
    }

    [Fact]
    public void Serialize_NonEmptyTags_IncludedInOutput()
    {
        // Arrange
        var ev = new Event { Type = "log", Tags = ["tag1"] };

        // Act
        string json = JsonSerializer.Serialize(ev, _jsonOptions);

        // Assert
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("tags", out var tagsEl));
        Assert.Equal(1, tagsEl.GetArrayLength());
    }

    [Fact]
    public void Serialize_EmptyReferences_OmittedFromOutput()
    {
        // Arrange
        var stack = new Stack { OrganizationId = "org1", ProjectId = "proj1", SignatureHash = "abc", Type = "error", Title = "test" };
        stack.References = new Collection<string>();

        // Act
        string json = JsonSerializer.Serialize(stack, _jsonOptions);

        // Assert
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("references", out _));
    }

    [Fact]
    public void Serialize_NonEmptyReferences_IncludedInOutput()
    {
        // Arrange
        var stack = new Stack { OrganizationId = "org1", ProjectId = "proj1", SignatureHash = "abc", Type = "error", Title = "test" };
        stack.References = new Collection<string>(["ref1"]);

        // Act
        string json = JsonSerializer.Serialize(stack, _jsonOptions);

        // Assert
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("references", out var refsEl));
        Assert.Equal(1, refsEl.GetArrayLength());
    }

    [Fact]
    public void Deserialize_MissingTags_DefaultsToEmpty()
    {
        // Arrange
        const string json = """{"type":"error","source":"test"}""";

        // Act
        var ev = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Tags);
        Assert.Empty(ev.Tags);
    }

    [Fact]
    public void Deserialize_MissingReferences_DefaultsToEmpty()
    {
        // Arrange
        const string json = """{"organization_id":"org1","project_id":"proj1","signature_hash":"abc","type":"error","title":"test"}""";

        // Act
        var stack = JsonSerializer.Deserialize<Stack>(json, _jsonOptions);

        // Assert
        Assert.NotNull(stack);
        Assert.NotNull(stack.References);
        Assert.Empty(stack.References);
    }

    [Fact]
    public void Roundtrip_EmptyCollections_PreservedOnDeserialize()
    {
        // Arrange
        var original = new Event { Type = "log", Tags = [], Data = new DataDictionary() };

        // Act
        string json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<Event>(json, _jsonOptions);

        // Assert
        Assert.DoesNotContain("tags", json);
        Assert.DoesNotContain("data", json);
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Tags);
        Assert.Empty(deserialized.Tags);
    }
}
