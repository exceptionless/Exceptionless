using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Core.Serialization;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public sealed class AuthenticationVersionSerializerTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public void Deserialize_LegacyRecords_PreservesMissingGeneration()
    {
        var serializer = GetService<ITextSerializer>();
        Assert.Null(serializer.Deserialize<User>("{}")!.AuthenticationVersion);
        Assert.Null(serializer.Deserialize<Token>("{}")!.AuthenticationVersion);
        Assert.Null(serializer.Deserialize<OAuthToken>("{}")!.AuthenticationVersion);
        var user = new User { AuthenticationVersion = "current" };
        Assert.Equal("current", serializer.Deserialize<User>(serializer.SerializeToString(user))!.AuthenticationVersion);
    }

    [Fact]
    public void UserVersion_CachePreservesMetadata_ApiOmitsMetadata()
    {
        var user = new User { Version = "1:42" };
        var serializer = GetService<ITextSerializer>();
        Assert.Equal(user.Version, serializer.Deserialize<User>(serializer.SerializeToString(user))!.Version);
        string apiJson = JsonSerializer.Serialize(user, new JsonSerializerOptions().ConfigureExceptionlessApiDefaults());
        Assert.DoesNotContain("\"version\"", apiJson);
    }
}
