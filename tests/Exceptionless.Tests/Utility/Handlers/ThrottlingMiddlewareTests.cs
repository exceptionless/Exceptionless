using System.Net;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Foundatio.Caching;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Exceptionless.Tests.Utility.Handlers;

public sealed class ThrottlingMiddlewareTests : TestWithServices
{
    public ThrottlingMiddlewareTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Invoke_AllowsRequestsUnderLimitAndAddsRateLimitHeaders()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, cache, maxRequests: 2);

        var context = CreateContext("/api/v2/events");
        var responseFeature = new CapturingHttpResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);

        // Act
        await middleware.Invoke(context);
        await responseFeature.FireOnStartingAsync();

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("2", context.Response.Headers[Headers.RateLimit]);
        Assert.Equal("1", context.Response.Headers[Headers.RateLimitRemaining]);
    }

    [Fact]
    public async Task Invoke_ReturnsTooManyRequestsWhenLimitExceeded()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        int nextCallCount = 0;
        var middleware = CreateMiddleware(context =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        }, cache, maxRequests: 1);

        // Act
        await middleware.Invoke(CreateContext("/api/v2/events"));
        var secondContext = CreateContext("/api/v2/events");
        await middleware.Invoke(secondContext);

        // Assert
        Assert.Equal(1, nextCallCount);
        Assert.Equal(StatusCodes.Status429TooManyRequests, secondContext.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_DoesNotThrottleKnownConfigurationAndHeartbeatRoutes()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        int nextCallCount = 0;
        var middleware = CreateMiddleware(context =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        }, cache, maxRequests: 0);

        // Act
        await middleware.Invoke(CreateContext("/api/v2/projects/config"));
        await middleware.Invoke(CreateContext("/api/v2/events/session/heartbeat"));

        // Assert
        Assert.Equal(2, nextCallCount);
    }

    [Fact]
    public async Task Invoke_V1ProjectConfigurationPath_DoesNotThrottleRequest()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, cache, maxRequests: 0);
        var context = CreateContext("/api/v1/project/config");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WebSocketPath_DoesNotThrottleRequest()
    {
        // Arrange
        var cache = GetService<ICacheClient>();
        bool nextCalled = false;
        var middleware = CreateMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, cache, maxRequests: 0);
        var context = CreateContext("/api/v2/push");

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private ThrottlingMiddleware CreateMiddleware(RequestDelegate next, ICacheClient cache, long maxRequests)
    {
        return new ThrottlingMiddleware(next, cache, new ThrottlingOptions
        {
            MaxRequestsForUserIdentifierFunc = _ => maxRequests,
            Period = TimeSpan.FromMinutes(1)
        }, TimeProvider);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CapturingHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _startingCallbacks = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _startingCallbacks.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            HasStarted = true;
            foreach (var (callback, state) in Enumerable.Reverse(_startingCallbacks))
                await callback(state);
        }
    }
}
