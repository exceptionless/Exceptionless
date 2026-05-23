using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class UserDescriptionSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public UserDescriptionSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"email_address":"test+tags@example.org","description":"Steps: 1. Open page 2. Click button 3. See error","data":{"screenshot":"base64data"}}""";

        // Act
        var result = _serializer.Deserialize<UserDescription>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test+tags@example.org", result.EmailAddress);
        Assert.Contains("Steps:", result.Description);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        // Arrange
        var desc = new UserDescription
        {
            EmailAddress = "user@example.com",
            Description = "The app crashed when I clicked the submit button.",
            Data = new DataDictionary
            {
                ["browser"] = "Chrome 120",
                ["page_url"] = "https://app.example.com/checkout"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(desc);
        var result = _serializer.Deserialize<UserDescription>(json);

        // Assert
        SerializerContractAssertions.IncludesProperties(json, "email_address");
        SerializerContractAssertions.ExcludesProperties(json, "EmailAddress");

        Assert.NotNull(result);
        Assert.Equal("user@example.com", result.EmailAddress);
        Assert.Equal("The app crashed when I clicked the submit button.", result.Description);
        Assert.NotNull(result.Data);
        Assert.Equal("Chrome 120", result.Data["browser"]);
    }

    [Fact]
    public void RoundTrip_WithMinimalProperties_PreservesValues()
    {
        // Arrange
        var desc = new UserDescription { Description = "It broke" };

        // Act
        string? json = _serializer.SerializeToString(desc);
        var result = _serializer.Deserialize<UserDescription>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("It broke", result.Description);
        Assert.Null(result.EmailAddress);
    }
}
