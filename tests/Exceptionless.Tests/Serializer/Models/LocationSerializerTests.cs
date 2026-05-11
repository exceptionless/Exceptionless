using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class LocationSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public LocationSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        // Arrange
        var location = new Location
        {
            Country = "United States",
            Level1 = "Texas",
            Level2 = "Travis County",
            Locality = "Austin"
        };

        // Act
        string? json = _serializer.SerializeToString(location);
        var result = _serializer.Deserialize<Location>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("United States", result.Country);
        Assert.Equal("Texas", result.Level1);
        Assert.Equal("Travis County", result.Level2);
        Assert.Equal("Austin", result.Locality);
    }

    [Fact]
    public void RoundTrip_WithPartialData_PreservesValues()
    {
        // Arrange
        var location = new Location { Country = "Germany" };

        // Act
        string? json = _serializer.SerializeToString(location);
        var result = _serializer.Deserialize<Location>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Germany", result.Country);
        Assert.Null(result.Level1);
        Assert.Null(result.Level2);
        Assert.Null(result.Locality);
    }

    [Fact]
    public void RoundTrip_WithUnicodeNames_PreservesValues()
    {
        // Arrange
        var location = new Location
        {
            Country = "日本",
            Level1 = "東京都",
            Locality = "渋谷区"
        };

        // Act
        string? json = _serializer.SerializeToString(location);
        var result = _serializer.Deserialize<Location>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("日本", result.Country);
        Assert.Equal("東京都", result.Level1);
        Assert.Equal("渋谷区", result.Locality);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"country":"Canada","level1":"Ontario","level2":"York Region","locality":"Toronto"}""";

        // Act
        var result = _serializer.Deserialize<Location>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Canada", result.Country);
        Assert.Equal("Ontario", result.Level1);
        Assert.Equal("York Region", result.Level2);
        Assert.Equal("Toronto", result.Locality);
    }
}
