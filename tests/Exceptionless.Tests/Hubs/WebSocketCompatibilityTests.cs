using System.Net.WebSockets;
using Exceptionless.Core;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Hubs;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class WebSocketConnectionCompatibilityTests : TestWithServices
{
    public WebSocketConnectionCompatibilityTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AddConnection_NewSocket_CanLookupAndEnumerateConnection()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket();

        string connectionId = manager.AddConnection(socket);

        Assert.False(String.IsNullOrEmpty(connectionId));
        Assert.Same(socket, manager.GetConnectionById(connectionId));
        Assert.Same(socket, Assert.Single(manager.GetAll()));
    }

    [Fact]
    public async Task RemoveConnectionAsync_ExistingConnection_RemovesAndClosesSocket()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket();
        string connectionId = manager.AddConnection(socket);

        await manager.RemoveConnectionAsync(connectionId);

        Assert.Null(manager.GetConnectionById(connectionId));
        Assert.Empty(manager.GetAll());
        Assert.Equal(1, socket.CloseCount);
        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public void SendMessage_ClosedSocket_ReturnsFalseAndRemovesConnection()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket(WebSocketState.Closed);
        string connectionId = manager.AddConnection(socket);

        bool sent = manager.SendMessage(connectionId, new { type = "test" });

        Assert.False(sent);
        Assert.Null(manager.GetConnectionById(connectionId));
    }

    private WebSocketConnectionManager CreateManager()
    {
        var options = new AppOptions { EnablePush = true };
        return new WebSocketConnectionManager(options, GetService<ITextSerializer>(), Log);
    }
}

public sealed class PushCompatibilityBrokerTests : TestWithServices
{
    private readonly MessageBusBroker _broker;
    private readonly IConnectionMapping _connectionMapping;
    private readonly SseConnectionManager _sseConnectionManager;
    private readonly WebSocketConnectionManager _webSocketConnectionManager;

    public PushCompatibilityBrokerTests(ITestOutputHelper output) : base(output)
    {
        _broker = GetService<MessageBusBroker>();
        _connectionMapping = GetService<IConnectionMapping>();
        _sseConnectionManager = GetService<SseConnectionManager>();
        _webSocketConnectionManager = GetService<WebSocketConnectionManager>();
    }

    [Fact]
    public async Task OnEntityChangedAsync_FansOutToSseAndWebSocketConnections()
    {
        const string organizationId = "compat-org";
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();
        var socket = new TestWebSocket();

        string sseConnectionId = "compat-sse";
        string webSocketConnectionId = _webSocketConnectionManager.AddConnection(socket);
        _sseConnectionManager.AddConnection(sseConnectionId, response, cts.Token);

        try
        {
            await _connectionMapping.GroupAddAsync(organizationId, sseConnectionId);
            await _connectionMapping.GroupAddAsync(organizationId, webSocketConnectionId);

            var entityChanged = new EntityChanged
            {
                Id = "stack-compat",
                Type = "Stack",
                ChangeType = ChangeType.Saved
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = organizationId;

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);
            await Task.Delay(200, TestContext.Current.CancellationToken);

            Assert.Contains("StackChanged", response.WrittenData);
            Assert.Single(socket.SentMessages);
            Assert.Contains("StackChanged", socket.SentMessages[0]);
        }
        finally
        {
            await _connectionMapping.GroupRemoveAsync(organizationId, sseConnectionId);
            await _connectionMapping.GroupRemoveAsync(organizationId, webSocketConnectionId);
            await _sseConnectionManager.RemoveConnectionAsync(sseConnectionId);
            await _webSocketConnectionManager.RemoveConnectionAsync(webSocketConnectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_ClosesWebSocketConnectionsAndClearsMapping()
    {
        const string userId = "compat-user";
        const string organizationId = "compat-org";
        var socket = new TestWebSocket();
        string connectionId = _webSocketConnectionManager.AddConnection(socket);

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, connectionId);
            await _connectionMapping.GroupAddAsync(organizationId, connectionId);

            var entityChanged = new EntityChanged
            {
                Id = "compat-token",
                Type = nameof(Token),
                ChangeType = ChangeType.Removed
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = organizationId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            Assert.Null(_webSocketConnectionManager.GetConnectionById(connectionId));
            Assert.Equal(1, socket.CloseCount);
            Assert.Empty(await _connectionMapping.GetUserIdConnectionsAsync(userId));
        }
        finally
        {
            await _connectionMapping.GroupRemoveAsync(organizationId, connectionId);
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId);
        }
    }
}
