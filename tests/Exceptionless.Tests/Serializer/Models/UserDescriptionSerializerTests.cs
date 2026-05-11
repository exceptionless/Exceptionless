using Exceptionless.Core.Extensions;
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
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
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

        string? json = _serializer.SerializeToString(desc);
        var result = _serializer.Deserialize<UserDescription>(json);

        Assert.NotNull(result);
        Assert.Equal("user@example.com", result.EmailAddress);
        Assert.Equal("The app crashed when I clicked the submit button.", result.Description);
        Assert.NotNull(result.Data);
        Assert.Equal("Chrome 120", result.Data["browser"]);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        /* language=json */
        const string json = """{"email_address":"test+tags@example.org","description":"Steps: 1. Open page 2. Click button 3. See error","data":{"screenshot":"base64data"}}""";

        var result = _serializer.Deserialize<UserDescription>(json);

        Assert.NotNull(result);
        Assert.Equal("test+tags@example.org", result.EmailAddress);
        Assert.Contains("Steps:", result.Description);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void RoundTrip_WithMinimalProperties_PreservesValues()
    {
        var desc = new UserDescription { Description = "It broke" };

        string? json = _serializer.SerializeToString(desc);
        var result = _serializer.Deserialize<UserDescription>(json);

        Assert.NotNull(result);
        Assert.Equal("It broke", result.Description);
        Assert.Null(result.EmailAddress);
    }

    [Fact]
    public void DataDictionary_GetValue_UserDescription_FromDictionary()
    {
        var dict = new DataDictionary
        {
            ["@user_description"] = new UserDescription
            {
                EmailAddress = "feedback@test.com",
                Description = "Needs improvement"
            }
        };

        var result = dict.GetValue<UserDescription>("@user_description", _serializer);

        Assert.NotNull(result);
        Assert.Equal("feedback@test.com", result.EmailAddress);
        Assert.Equal("Needs improvement", result.Description);
    }
}
