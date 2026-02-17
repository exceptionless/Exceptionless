using System.Text.Json;
using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests ClientConfiguration serialization through ITextSerializer.
/// Critical: Settings property uses init accessor. STJ must populate the
/// SettingsDictionary during deserialization so settings survive round-trips.
/// This is the exact bug that caused empty settings in production.
/// </summary>
public class ClientConfigurationSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ClientConfigurationSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesSettings()
    {
        // Arrange
        var config = new ClientConfiguration { Version = 5 };
        config.Settings["IncludeConditionalData"] = "true";
        config.Settings["DataExclusions"] = "password";

        // Act
        string json = _serializer.SerializeToString(config);
        var deserialized = _serializer.Deserialize<ClientConfiguration>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(5, deserialized.Version);
        Assert.Equal(2, deserialized.Settings.Count);
        Assert.True(deserialized.Settings.GetBoolean("IncludeConditionalData"));
        Assert.Equal("password", deserialized.Settings.GetString("DataExclusions"));
    }

    [Fact]
    public void SerializeToString_UsesSnakeCasePropertyNames()
    {
        // Arrange
        var config = new ClientConfiguration { Version = 3 };
        config.Settings["TestKey"] = "TestValue";

        // Act
        string json = _serializer.SerializeToString(config);

        // Assert — property names should be snake_case, dictionary keys preserved
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("version", out _), "Expected snake_case 'version'");
        Assert.False(root.TryGetProperty("Version", out _), "Should not have PascalCase 'Version'");
        Assert.True(root.TryGetProperty("settings", out var settings), "Expected snake_case 'settings'");
        Assert.True(settings.TryGetProperty("TestKey", out var testVal), "Dictionary keys should preserve original casing");
        Assert.Equal("TestValue", testVal.GetString());
    }

    [Fact]
    public void Deserialize_EmptySettings_ReturnsEmptyDictionary()
    {
        // Arrange
        /* language=json */
        const string json = """{"version":1,"settings":{}}""";

        // Act
        var config = _serializer.Deserialize<ClientConfiguration>(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(1, config.Version);
        Assert.NotNull(config.Settings);
        Assert.Empty(config.Settings);
    }

    [Fact]
    public void Deserialize_MissingSettings_DefaultsToEmptyDictionary()
    {
        // Arrange
        /* language=json */
        const string json = """{"version":2}""";

        // Act
        var config = _serializer.Deserialize<ClientConfiguration>(json);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(2, config.Version);
        Assert.NotNull(config.Settings);
        Assert.Empty(config.Settings);
    }
}
