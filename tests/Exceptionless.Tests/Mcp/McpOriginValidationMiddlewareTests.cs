using Exceptionless.Core;
using Exceptionless.Core.Services;
using Exceptionless.Web.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Mcp;

public sealed class McpOriginValidationMiddlewareTests
{
    private static readonly IReadOnlySet<string> AllowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "https://api-ex.dev.localhost:7111",
        "https://web-ex.dev.localhost:7131"
    };

    [Fact]
    public async Task InvokeAsync_ConfiguredOrigins_InvokeNextMiddleware()
    {
        // Arrange
        int nextInvocationCount = 0;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:AllowedOrigins:App0"] = "https://web-ex.dev.localhost:7131",
                ["Mcp:AllowedOrigins:App1"] = "https://app.exceptionless.io",
                ["Mcp:AllowedOrigins:Api0"] = "https://api.exceptionless.io"
            })
            .Build();
        var middleware = new McpOriginValidationMiddleware(
            _ =>
            {
                nextInvocationCount++;
                return Task.CompletedTask;
            },
            new AppOptions { BaseURL = "http://localhost:9001/#!" },
            configuration);
        DefaultHttpContext[] contexts =
        [
            CreateContext("https://web-ex.dev.localhost:7131"),
            CreateContext("https://app.exceptionless.io"),
            CreateContext("https://api.exceptionless.io"),
            CreateContext("http://localhost:9001")
        ];

        // Act
        foreach (var context in contexts)
            await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(4, nextInvocationCount);
    }

    [Theory]
    [InlineData("https://api-ex.dev.localhost:7111")]
    [InlineData("https://web-ex.dev.localhost:7131")]
    [InlineData("HTTPS://WEB-EX.DEV.LOCALHOST:7131")]
    public void IsAllowedOrigin_ConfiguredOrigin_ReturnsTrue(string origin)
    {
        // Arrange is provided by the theory data.

        // Act
        bool isAllowed = McpOriginValidationMiddleware.IsAllowedOrigin(origin, AllowedOrigins);

        // Assert
        Assert.True(isAllowed);
    }

    [Theory]
    [InlineData("https://attacker.example")]
    [InlineData("https://web-ex.dev.localhost:7132")]
    [InlineData("https://web-ex.dev.localhost:7131/path")]
    [InlineData("https://user@web-ex.dev.localhost:7131")]
    [InlineData("not-an-origin")]
    public void IsAllowedOrigin_UntrustedOrMalformedOrigin_ReturnsFalse(string origin)
    {
        // Arrange is provided by the theory data.

        // Act
        bool isAllowed = McpOriginValidationMiddleware.IsAllowedOrigin(origin, AllowedOrigins);

        // Assert
        Assert.False(isAllowed);
    }

    private static DefaultHttpContext CreateContext(string origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = OAuthService.McpResource.Path;
        context.Request.Headers.Origin = origin;
        return context;
    }
}
