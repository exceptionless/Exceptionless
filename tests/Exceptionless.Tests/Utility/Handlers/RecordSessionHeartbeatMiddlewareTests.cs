using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Utility;
using Foundatio.Caching;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class RecordSessionHeartbeatMiddlewareTests : TestWithServices
{
    public RecordSessionHeartbeatMiddlewareTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Invoke_NonHeartbeatRouteCallsNext()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v2/events");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_HeartbeatWithoutProjectReturnsUnauthorized()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateContext("/api/v2/events/session/heartbeat?id=session-1");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_HeartbeatWithoutIdReturnsOkWithoutRecording()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext("/api/v2/events/session/heartbeat");

        // Act
        await middleware.Invoke(context);

        // Assert
        string cacheKey = $"Project:{TestConstants.ProjectId}:heartbeat:{"session-1".ToSHA1()}";
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(default, await cache.GetAsync<DateTime>(cacheKey, default));
    }

    [Fact]
    public async Task Invoke_HeartbeatRecordsSessionAndCloseFlag()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(utcNow);

        var cache = GetService<ICacheClient>();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateAuthenticatedContext("/api/v2/events/session/heartbeat?id=session-1&close=true");

        // Act
        await middleware.Invoke(context);

        // Assert
        string cacheKey = $"Project:{TestConstants.ProjectId}:heartbeat:{"session-1".ToSHA1()}";
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(utcNow, await cache.GetAsync<DateTime>(cacheKey, default));
        Assert.True(await cache.GetAsync<bool>($"{cacheKey}-close", false));
    }

    private RecordSessionHeartbeatMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new RecordSessionHeartbeatMiddleware(
            next,
            GetService<ICacheClient>(),
            GetService<AppOptions>(),
            TimeProvider,
            GetService<ILogger<ProjectConfigMiddleware>>());
    }

    private static DefaultHttpContext CreateContext(string pathAndQuery)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        string[] parts = pathAndQuery.Split('?', 2);
        context.Request.Path = parts[0];
        if (parts.Length == 2)
            context.Request.QueryString = new QueryString("?" + parts[1]);

        return context;
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string pathAndQuery)
    {
        var context = CreateContext(pathAndQuery);
        var token = new Token
        {
            Id = TestConstants.ApiKey,
            Type = TokenType.Access,
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId
        };

        context.User = new ClaimsPrincipal(token.ToIdentity());
        return context;
    }
}
