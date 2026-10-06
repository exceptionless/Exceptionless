using Exceptionless.Web.Security;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class ApiContentSecurityPolicyTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData(" https://api.localhost:9443/backend?ignored=true#ignored ", "https://api.localhost:9443", "wss://api.localhost:9443")]
    [InlineData("http://api.localhost:8111/backend", "http://api.localhost:8111", "ws://api.localhost:8111")]
    [InlineData("HTTPS://API.LOCALHOST:443/backend", "https://api.localhost", "wss://api.localhost")]
    [InlineData("http://[::1]:8111/backend", "http://[::1]:8111", "ws://[::1]:8111")]
    public void GetConfiguredOrigins_HttpUrl_AllowsOnlyHttpAndWebSocketOrigins(string apiUrl, string httpOrigin, string webSocketOrigin)
    {
        // Arrange: configuration is supplied by the theory.

        // Act
        string[] origins = ApiContentSecurityPolicy.GetConfiguredOrigins(apiUrl);

        // Assert
        Assert.Equal([httpOrigin, webSocketOrigin], origins);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/api")]
    [InlineData("//api.localhost")]
    [InlineData("ftp://api.localhost")]
    [InlineData("wss://api.localhost")]
    [InlineData("https://user:password@api.localhost")]
    [InlineData("https://*.example.test")]
    [InlineData("https://api.localhost;script-src")]
    public void GetConfiguredOrigins_InvalidConfiguration_DoesNotExpandPolicy(string? apiUrl)
    {
        // Arrange: configuration is supplied by the theory.

        // Act
        string[] origins = ApiContentSecurityPolicy.GetConfiguredOrigins(apiUrl);

        // Assert
        Assert.Empty(origins);
    }
}
