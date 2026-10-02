using Exceptionless.Core.Models;
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
}
