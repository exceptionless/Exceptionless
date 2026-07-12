using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Web.Hubs;
using Foundatio.Repositories.Models;
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
}
