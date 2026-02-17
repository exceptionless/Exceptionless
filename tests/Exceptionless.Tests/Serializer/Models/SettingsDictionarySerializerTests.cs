using System.Text.Json;
using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests SettingsDictionary serialization through ITextSerializer.
/// Critical: SettingsDictionary extends ObservableDictionary which implements
/// IDictionary via composition (not inheritance). STJ must serialize it as a
/// flat dictionary, not as an empty object.
/// </summary>
public class SettingsDictionarySerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public SettingsDictionarySerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_WithEntries_SerializesAsFlatDictionary()
    {
        // Arrange
        var settings = new SettingsDictionary
        {
            { "IncludeConditionalData", "true" },
            { "DataExclusions", "password,secret" }
        };

        // Act
        string json = _serializer.SerializeToString(settings);

        // Assert — should be a flat dictionary, not a wrapped object
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("IncludeConditionalData", out var includeVal));
        Assert.Equal("true", includeVal.GetString());
        Assert.True(root.TryGetProperty("DataExclusions", out var exclusionsVal));
        Assert.Equal("password,secret", exclusionsVal.GetString());
    }

    [Fact]
    public void Deserialize_FlatDictionaryJson_PopulatesEntries()
    {
        // Arrange
        /* language=json */
        const string json = """{"IncludeConditionalData":"true","DataExclusions":"password,secret"}""";

        // Act
        var settings = _serializer.Deserialize<SettingsDictionary>(json);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(2, settings.Count);
        Assert.Equal("true", settings.GetString("IncludeConditionalData"));
        Assert.Equal("password,secret", settings.GetString("DataExclusions"));
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesAllEntries()
    {
        // Arrange
        var original = new SettingsDictionary
        {
            { "BoolSetting", "true" },
            { "IntSetting", "42" },
            { "StringSetting", "hello" }
        };

        // Act
        string json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<SettingsDictionary>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Count, deserialized.Count);
        Assert.True(deserialized.GetBoolean("BoolSetting"));
        Assert.Equal(42, deserialized.GetInt32("IntSetting"));
        Assert.Equal("hello", deserialized.GetString("StringSetting"));
    }

    [Fact]
    public void Deserialize_EmptyDictionary_ReturnsEmptySettings()
    {
        // Arrange
        /* language=json */
        const string json = """{}""";

        // Act
        var settings = _serializer.Deserialize<SettingsDictionary>(json);

        // Assert
        Assert.NotNull(settings);
        Assert.Empty(settings);
    }

    [Fact]
    public void SerializeToString_PreservesOriginalKeyCasing()
    {
        // Arrange — dictionary keys should NOT be snake_cased
        var settings = new SettingsDictionary
        {
            { "@@DataExclusions", "password" },
            { "IncludePrivateInformation", "true" },
            { "MyCustomSetting", "value" }
        };

        // Act
        string json = _serializer.SerializeToString(settings);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("@@DataExclusions", out _));
        Assert.True(root.TryGetProperty("IncludePrivateInformation", out _));
        Assert.True(root.TryGetProperty("MyCustomSetting", out _));
    }
}
