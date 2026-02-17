using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests ModuleCollection serialization through ITextSerializer.
/// ModuleCollection extends Collection&lt;Module&gt; directly.
/// </summary>
public class ModuleCollectionSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ModuleCollectionSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesAllModules()
    {
        // Arrange
        var collection = new ModuleCollection
        {
            new()
            {
                ModuleId = 1,
                Name = "Exceptionless.Core",
                Version = "8.1.0",
                IsEntry = true,
                CreatedDate = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 2, 10, 14, 0, 0, DateTimeKind.Utc),
                Data = new DataDictionary { ["PublicKeyToken"] = "b77a5c561934e089" }
            },
            new()
            {
                ModuleId = 2,
                Name = "Foundatio",
                Version = "11.0.0",
                IsEntry = false
            }
        };

        // Act
        string json = _serializer.SerializeToString(collection);
        var deserialized = _serializer.Deserialize<ModuleCollection>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);

        Assert.Equal(1, deserialized[0].ModuleId);
        Assert.Equal("Exceptionless.Core", deserialized[0].Name);
        Assert.Equal("8.1.0", deserialized[0].Version);
        Assert.True(deserialized[0].IsEntry);
        Assert.NotNull(deserialized[0].CreatedDate);
        Assert.NotNull(deserialized[0].ModifiedDate);
        Assert.NotNull(deserialized[0].Data);

        Assert.Equal(2, deserialized[1].ModuleId);
        Assert.Equal("Foundatio", deserialized[1].Name);
        Assert.False(deserialized[1].IsEntry);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyCollection()
    {
        // Arrange
        /* language=json */
        const string json = """[]""";

        // Act
        var collection = _serializer.Deserialize<ModuleCollection>(json);

        // Assert
        Assert.NotNull(collection);
        Assert.Empty(collection);
    }

    [Fact]
    public void SerializeToString_UsesSnakeCasePropertyNames()
    {
        // Arrange
        var collection = new ModuleCollection
        {
            new()
            {
                ModuleId = 42,
                Name = "System.Runtime",
                IsEntry = false,
                CreatedDate = DateTime.UtcNow
            }
        };

        // Act
        string json = _serializer.SerializeToString(collection);

        // Assert
        Assert.Contains("module_id", json);
        Assert.Contains("is_entry", json);
        Assert.Contains("created_date", json);
        Assert.DoesNotContain("ModuleId", json);
        Assert.DoesNotContain("IsEntry", json);
        Assert.DoesNotContain("CreatedDate", json);
    }
}
