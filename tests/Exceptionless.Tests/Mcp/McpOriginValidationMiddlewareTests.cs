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
    public async Task InvokeAsync_ConfiguredOrigin_InvokesNextMiddleware()
    {
        // Arrange
        bool nextInvoked = false;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:AllowedOrigins:0"] = "https://web-ex.dev.localhost:7131"
            })
            .Build();
        var middleware = new McpOriginValidationMiddleware(
            _ =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            },
            new AppOptions { BaseURL = "https://api-ex.dev.localhost:7111" },
            configuration);
        var context = new DefaultHttpContext();
        context.Request.Path = OAuthService.McpResource.Path;
        context.Request.Headers.Origin = "https://web-ex.dev.localhost:7131";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextInvoked);
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
}
