using System.Net.WebSockets;
using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Hubs;
using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Exceptionless.Tests.Hubs;

/// <summary>
/// Tests for <see cref="MessageBusBroker"/> WebSocket behavior. Calls
/// <see cref="MessageBusBroker.OnEntityChangedAsync"/> directly so they do not depend on
/// message bus wiring or <c>EnablePush</c> in test host configuration.
/// </summary>
public sealed class WebSocketTests : TestWithServices
{
    private readonly MessageBusBroker _broker;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly PushConnectionRegistry _connectionRegistry;

    public WebSocketTests(ITestOutputHelper output) : base(output)
    {
        _broker = GetService<MessageBusBroker>();
        _connectionManager = GetService<WebSocketConnectionManager>();
        _connectionRegistry = GetService<PushConnectionRegistry>();
    }

    [Fact]
    public async Task Invoke_UnauthenticatedPushRequest_ClosesWithExplicitUnauthorizedStatus()
    {
        var socket = new TestWebSocket();
        var feature = new TestWebSocketFeature(socket);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v2/push";
        context.Features.Set<IHttpWebSocketFeature>(feature);
        bool calledNext = false;
        var middleware = CreateMiddleware(
            _ =>
            {
                calledNext = true;
                return Task.CompletedTask;
            });

        await middleware.Invoke(context);

        Assert.False(calledNext);
        Assert.True(feature.WasAccepted);
        Assert.Equal(0, socket.CloseCount);
        Assert.Equal(1, socket.CloseOutputCount);
        Assert.Equal((WebSocketCloseStatus)4401, socket.RequestedCloseStatus);
        Assert.Equal("Unauthorized", socket.RequestedCloseStatusDescription);
    }

    [Fact]
    public async Task Invoke_TokenRevokedWhileAccepting_DoesNotLeaveUntrackedConnection()
    {
        const string userId = "accept-race-user";
        const string tokenId = "accept-race-token";
        const string organizationId = "accept-race-organization";
        using var requestAborted = new CancellationTokenSource();
        var socket = new TestWebSocket(blockReceive: true);
        var feature = new BlockingTestWebSocketFeature(socket);
        var context = new DefaultHttpContext
        {
            RequestAborted = requestAborted.Token,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(IdentityUtils.LoggedInUsersTokenId, tokenId),
                new Claim(IdentityUtils.OrganizationIdsClaim, organizationId)
            ], IdentityUtils.UserAuthenticationType))
        };
        context.Request.Path = "/api/v2/push";
        context.Features.Set<IHttpWebSocketFeature>(feature);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        Task invokeTask = middleware.Invoke(context);
        await feature.WaitUntilAcceptingAsync();

        var entityChanged = new EntityChanged
        {
            Id = tokenId,
            Type = nameof(Token),
            ChangeType = ChangeType.Removed
        };
        entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
        entityChanged.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;
        await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

        feature.CompleteAccept();

        try
        {
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.Empty(_connectionManager.GetAll());
            Assert.Empty(_connectionRegistry.GetUserConnections(userId));
            Assert.Equal((WebSocketCloseStatus)4401, socket.RequestedCloseStatus);
        }
        finally
        {
            await requestAborted.CancelAsync();
            await invokeTask;
        }
    }

    [Fact]
    public async Task Invoke_TokenPrincipal_PreservesOrganizationOnlyCompatibilityConnection()
    {
        const string tokenId = "project-access-token";
        const string organizationId = "token-organization";
        using var requestAborted = new CancellationTokenSource();
        var socket = new TestWebSocket(blockReceive: true);
        var feature = new TestWebSocketFeature(socket);
        var context = new DefaultHttpContext
        {
            RequestAborted = requestAborted.Token,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, tokenId),
                new Claim(IdentityUtils.OrganizationIdsClaim, organizationId)
            ], IdentityUtils.TokenAuthenticationType))
        };
        context.Request.Path = "/api/v2/push";
        context.Features.Set<IHttpWebSocketFeature>(feature);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        Task invokeTask = middleware.Invoke(context);
        await socket.WaitUntilReceivingAsync();

        try
        {
            Assert.False(invokeTask.IsCompleted);
            Assert.Same(socket, Assert.Single(_connectionManager.GetAll()));
            Assert.Single(_connectionRegistry.GetGroupConnections(organizationId));
            Assert.Empty(_connectionRegistry.GetUserConnections(tokenId));
        }
        finally
        {
            await requestAborted.CancelAsync();
            await invokeTask;
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_ClosesWebSocketsAndClearsUserMapping()
    {
        const string userId = "test-user-id";
        const string organizationId = "test-organization-id";
        var socket1 = new TestWebSocket();
        var socket2 = new TestWebSocket();
        var unrelatedSocket = new TestWebSocket();

        string connectionId1 = _connectionManager.AddWebSocket(socket1);
        string connectionId2 = _connectionManager.AddWebSocket(socket2);
        string unrelatedConnectionId = _connectionManager.AddWebSocket(unrelatedSocket);
        Assert.True(_connectionRegistry.TryRegister(connectionId1, userId, "test-token-id", [organizationId]));
        Assert.True(_connectionRegistry.TryRegister(connectionId2, userId, "test-token-id", [organizationId]));
        Assert.True(_connectionRegistry.TryRegister(unrelatedConnectionId, "unrelated-user", "unrelated-token-id", [organizationId]));

        try
        {
            var entityChanged = new EntityChanged
            {
                Id = "test-token-id",
                Type = nameof(Token),
                ChangeType = ChangeType.Removed
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = organizationId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            Assert.Null(_connectionManager.GetWebSocketById(connectionId1));
            Assert.Null(_connectionManager.GetWebSocketById(connectionId2));
            Assert.Same(unrelatedSocket, _connectionManager.GetWebSocketById(unrelatedConnectionId));

            Assert.Equal(1, socket1.CloseCount);
            Assert.Equal(1, socket2.CloseCount);
            Assert.Equal(0, unrelatedSocket.CloseCount);

            Assert.Empty(_connectionRegistry.GetUserConnections(userId));
            var organizationConnections = _connectionRegistry.GetGroupConnections(organizationId);
            Assert.DoesNotContain(connectionId1, organizationConnections);
            Assert.DoesNotContain(connectionId2, organizationConnections);
            Assert.Contains(unrelatedConnectionId, organizationConnections);
        }
        finally
        {
            await _connectionManager.RemoveWebSocketAsync(unrelatedConnectionId);
            _connectionRegistry.Unregister(connectionId1);
            _connectionRegistry.Unregister(connectionId2);
            _connectionRegistry.Unregister(unrelatedConnectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_NonAuthTokenRemoved_DoesNotCloseWebSockets()
    {
        const string userId = "test-user-id-2";
        var socket = new TestWebSocket();
        string connectionId = _connectionManager.AddWebSocket(socket);
        Assert.True(_connectionRegistry.TryRegister(connectionId, userId, "authentication-token", []));

        try
        {
            var entityChanged = new EntityChanged
            {
                Id = "test-api-token-id",
                Type = nameof(Token),
                ChangeType = ChangeType.Removed
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            Assert.Equal(0, socket.CloseCount);
            Assert.Same(socket, _connectionManager.GetWebSocketById(connectionId));
        }
        finally
        {
            await _connectionManager.RemoveWebSocketAsync(connectionId);
            _connectionRegistry.Unregister(connectionId);
        }
    }

    private sealed class TestWebSocketFeature(WebSocket socket) : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;
        public bool WasAccepted { get; private set; }

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            WasAccepted = true;
            return Task.FromResult(socket);
        }
    }

    private sealed class BlockingTestWebSocketFeature(WebSocket socket) : IHttpWebSocketFeature
    {
        private readonly TaskCompletionSource _accepting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completeAccept = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsWebSocketRequest => true;

        public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            _accepting.TrySetResult();
            await _completeAccept.Task;
            return socket;
        }

        public void CompleteAccept() => _completeAccept.TrySetResult();
        public Task WaitUntilAcceptingAsync() => _accepting.Task;
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }

    private WebSocketPushMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new WebSocketPushMiddleware(
            next,
            _connectionManager,
            new ConnectionLeaseStore(TimeProvider),
            _connectionRegistry,
            TimeProvider,
            new TestHostApplicationLifetime(),
            GetService<ILogger<WebSocketPushMiddleware>>());
    }
}
