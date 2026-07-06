using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Mcp;
using Foundatio.Caching;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Exceptionless.Tests.Mcp;

public sealed class McpSessionMigrationHandlerTests : TestWithServices
{
    public McpSessionMigrationHandlerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task AllowSessionMigrationAsync_SameCaller_ReturnsStoredInitializeParams()
    {
        var handler = CreateHandler();
        string sessionId = Guid.NewGuid().ToString("N");
        var context = CreateContext();
        var initializeParams = CreateInitializeParams();

        await handler.OnSessionInitializedAsync(context, sessionId, initializeParams, CancellationToken.None);

        var restored = await handler.AllowSessionMigrationAsync(context, sessionId, CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(initializeParams.ProtocolVersion, restored.ProtocolVersion);
        Assert.Equal(initializeParams.ClientInfo.Name, restored.ClientInfo.Name);
        Assert.Equal(initializeParams.ClientInfo.Version, restored.ClientInfo.Version);
    }

    [Fact]
    public async Task AllowSessionMigrationAsync_DifferentUser_ReturnsNull()
    {
        var handler = CreateHandler();
        string sessionId = Guid.NewGuid().ToString("N");
        var initializeContext = CreateContext(userId: "user-a");
        var migrationContext = CreateContext(userId: "user-b");

        await handler.OnSessionInitializedAsync(initializeContext, sessionId, CreateInitializeParams(), CancellationToken.None);

        var restored = await handler.AllowSessionMigrationAsync(migrationContext, sessionId, CancellationToken.None);

        Assert.Null(restored);
    }

    [Fact]
    public async Task AllowSessionMigrationAsync_DifferentOAuthClient_ReturnsNull()
    {
        var handler = CreateHandler();
        string sessionId = Guid.NewGuid().ToString("N");
        var initializeContext = CreateContext(clientId: "client-a");
        var migrationContext = CreateContext(clientId: "client-b");

        await handler.OnSessionInitializedAsync(initializeContext, sessionId, CreateInitializeParams(), CancellationToken.None);

        var restored = await handler.AllowSessionMigrationAsync(migrationContext, sessionId, CancellationToken.None);

        Assert.Null(restored);
    }

    [Fact]
    public async Task AllowSessionMigrationAsync_MissingMcpReadRole_ReturnsNull()
    {
        var handler = CreateHandler();
        string sessionId = Guid.NewGuid().ToString("N");
        var initializeContext = CreateContext();
        var migrationContext = CreateContext(includeMcpReadRole: false);

        await handler.OnSessionInitializedAsync(initializeContext, sessionId, CreateInitializeParams(), CancellationToken.None);

        var restored = await handler.AllowSessionMigrationAsync(migrationContext, sessionId, CancellationToken.None);

        Assert.Null(restored);
    }

    private McpSessionMigrationHandler CreateHandler()
    {
        return new McpSessionMigrationHandler(
            GetService<ICacheClient>(),
            GetService<ITextSerializer>(),
            Options.Create(new HttpServerTransportOptions { IdleTimeout = TimeSpan.FromMinutes(30) }),
            TimeProvider,
            GetService<ILogger<McpSessionMigrationHandler>>());
    }

    private static DefaultHttpContext CreateContext(
        string userId = "user-1",
        string clientId = "test-client",
        string resource = "http://localhost/mcp",
        bool includeMcpReadRole = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(IdentityUtils.OAuthClientIdClaim, clientId),
            new(IdentityUtils.OAuthResourceClaim, resource)
        };

        if (includeMcpReadRole)
            claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.McpRead));

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityUtils.TokenAuthenticationType))
        };
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/mcp";
        return context;
    }

    private static InitializeRequestParams CreateInitializeParams()
    {
        return new InitializeRequestParams
        {
            ProtocolVersion = "2025-06-18",
            Capabilities = new ClientCapabilities(),
            ClientInfo = new Implementation
            {
                Name = "codex",
                Version = "1.0.0"
            }
        };
    }
}
