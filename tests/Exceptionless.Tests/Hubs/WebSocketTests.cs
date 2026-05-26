using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Hubs;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Hubs;

/// <summary>
/// Tests for <see cref="MessageBusBroker"/> WebSocket behavior.  Calls
/// <see cref="MessageBusBroker.OnEntityChangedAsync"/> directly so they do not depend on
/// message bus wiring or <c>EnableWebSockets</c> in test host configuration.
/// </summary>
public sealed class WebSocketTests : TestWithServices
{
    private readonly MessageBusBroker _broker;
    private readonly IConnectionMapping _connectionMapping;
    private readonly WebSocketConnectionManager _connectionManager;

    public WebSocketTests(ITestOutputHelper output) : base(output)
    {
        _broker = GetService<MessageBusBroker>();
        _connectionMapping = GetService<IConnectionMapping>();
        _connectionManager = GetService<WebSocketConnectionManager>();
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_ClosesWebSocketsAndClearsUserMapping()
    {
        // Arrange
        const string userId = "test-user-id";
        const string organizationId = "test-organization-id";
        var socket1 = new TestWebSocket();
        var socket2 = new TestWebSocket();
        var unrelatedSocket = new TestWebSocket();

        string connectionId1 = _connectionManager.AddWebSocket(socket1);
        string connectionId2 = _connectionManager.AddWebSocket(socket2);
        string unrelatedConnectionId = _connectionManager.AddWebSocket(unrelatedSocket);

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, connectionId1);
            await _connectionMapping.UserIdAddAsync(userId, connectionId2);
            await _connectionMapping.GroupAddAsync(organizationId, connectionId1);
            await _connectionMapping.GroupAddAsync(organizationId, connectionId2);
            await _connectionMapping.GroupAddAsync(organizationId, unrelatedConnectionId);

            var entityChanged = new EntityChanged
            {
                Id = "test-token-id",
                Type = nameof(Token),
                ChangeType = ChangeType.Removed
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = organizationId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;

            // Act — call the broker directly; no message bus or EnableWebSockets dependency
            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            // Assert – sockets closed and removed from manager
            Assert.Null(_connectionManager.GetWebSocketById(connectionId1));
            Assert.Null(_connectionManager.GetWebSocketById(connectionId2));
            Assert.Same(unrelatedSocket, _connectionManager.GetWebSocketById(unrelatedConnectionId));

            Assert.Equal(1, socket1.CloseCount);
            Assert.Equal(1, socket2.CloseCount);
            Assert.Equal(0, unrelatedSocket.CloseCount);

            // Assert – user-id mapping removed by broker
            var remaining = await _connectionMapping.GetUserIdConnectionsAsync(userId);
            Assert.Empty(remaining);
            var organizationConnections = await _connectionMapping.GetGroupConnectionsAsync(organizationId);
            Assert.DoesNotContain(connectionId1, organizationConnections);
            Assert.DoesNotContain(connectionId2, organizationConnections);
            Assert.Contains(unrelatedConnectionId, organizationConnections);
        }
        finally
        {
            await _connectionMapping.GroupRemoveAsync(organizationId, unrelatedConnectionId);
            await _connectionManager.RemoveWebSocketAsync(unrelatedConnectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_NonAuthTokenRemoved_DoesNotCloseWebSockets()
    {
        // Arrange
        const string userId = "test-user-id-2";
        var socket = new TestWebSocket();
        string connectionId = _connectionManager.AddWebSocket(socket);

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, connectionId);

            var entityChanged = new EntityChanged
            {
                Id = "test-api-token-id",
                Type = nameof(Token),
                ChangeType = ChangeType.Removed
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
            // IsAuthenticationToken intentionally omitted (defaults false)

            // Act
            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            // Assert – socket should NOT be closed for a non-auth token removal
            Assert.Equal(0, socket.CloseCount);
            Assert.Same(socket, _connectionManager.GetWebSocketById(connectionId));
        }
        finally
        {
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId);
            await _connectionManager.RemoveWebSocketAsync(connectionId);
        }
    }
}
