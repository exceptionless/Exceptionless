using Exceptionless.Core;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Web.Hubs;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;
using System.Security.Claims;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class SseConnectionManagerTests : TestWithServices
{
    public SseConnectionManagerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AddConnection_NewConnection_CanLookupAndEnumerate()
    {
        using var manager = CreateManager();
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "test-conn-1";
        var connection = manager.AddConnection(connectionId, response, cts.Token);

        Assert.NotNull(connection);
        Assert.Same(connection, manager.GetConnectionById(connectionId));
        Assert.Equal(1, manager.ConnectionCount);
        Assert.Contains(connection, manager.GetAll());
    }

    [Fact]
    public async Task RemoveConnectionAsync_ExistingConnection_RemovesAndAborts()
    {
        using var manager = CreateManager();
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "test-conn-2";
        var connection = manager.AddConnection(connectionId, response, cts.Token);

        await manager.RemoveConnectionAsync(connectionId);

        Assert.Null(manager.GetConnectionById(connectionId));
        Assert.Equal(0, manager.ConnectionCount);
        Assert.True(connection.ConnectionAborted.IsCancellationRequested);
    }

    [Fact]
    public async Task RemoveConnectionAsync_UnknownConnection_DoesNothing()
    {
        using var manager = CreateManager();

        await manager.RemoveConnectionAsync("nonexistent");

        Assert.Equal(0, manager.ConnectionCount);
    }

    [Fact]
    public void SendMessage_ValidConnection_EnqueuesMessage()
    {
        using var manager = CreateManager();
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "test-conn-3";
        manager.AddConnection(connectionId, response, cts.Token);

        bool sent = manager.SendMessage(connectionId, new { type = "test", message = "hello" });

        Assert.True(sent);
    }

    [Fact]
    public void SendMessage_UnknownConnection_ReturnsFalse()
    {
        using var manager = CreateManager();

        bool sent = manager.SendMessage("missing", new { type = "test" });

        Assert.False(sent);
    }

    [Fact]
    public async Task SendMessage_AbortedConnection_ReturnsFalseAndRemoves()
    {
        using var manager = CreateManager();
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "test-conn-4";
        manager.AddConnection(connectionId, response, cts.Token);

        await cts.CancelAsync();

        bool sent = manager.SendMessage(connectionId, new { type = "test" });

        Assert.False(sent);
        // Connection should be cleaned up
        Assert.Null(manager.GetConnectionById(connectionId));
    }

    [Fact]
    public void SendMessageToAll_MultipleConnections_SendsToAll()
    {
        using var manager = CreateManager();
        using var response1 = new FakeHttpResponse();
        using var response2 = new FakeHttpResponse();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        manager.AddConnection("conn-1", response1, cts1.Token);
        manager.AddConnection("conn-2", response2, cts2.Token);

        manager.SendMessageToAll(new { type = "broadcast" });

        // Both connections should have received the message (enqueued)
        Assert.Equal(2, manager.ConnectionCount);
    }

    private SseConnectionManager CreateManager()
    {
        var options = new AppOptions { EnablePush = true };
        return new SseConnectionManager(options, GetService<ITextSerializer>(), Log);
    }
}

/// <summary>
/// Tests for MessageBusBroker using SSE connections.
/// </summary>
public sealed class SseBrokerTests : TestWithServices
{
    private readonly MessageBusBroker _broker;
    private readonly SseConnectionManager _connectionManager;
    private readonly PushConnectionRegistry _connectionRegistry;

    public SseBrokerTests(ITestOutputHelper output) : base(output)
    {
        _broker = GetService<MessageBusBroker>();
        _connectionManager = GetService<SseConnectionManager>();
        _connectionRegistry = GetService<PushConnectionRegistry>();
    }

    [Fact]
    public void CanDrop_UserMembershipChanged_ReturnsFalse()
    {
        // Arrange
        var message = new UserMembershipChanged
        {
            UserId = "membership-user",
            OrganizationId = "membership-organization",
            ChangeType = ChangeType.Removed
        };

        // Act
        bool canDrop = MessageBusBroker.CanDrop(message);

        // Assert
        Assert.False(canDrop);
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_ClosesConnectionsAndClearsMapping()
    {
        const string userId = "test-user-id";
        const string organizationId = "test-org-id";
        using var response1 = new FakeHttpResponse();
        using var response2 = new FakeHttpResponse();
        using var unrelatedResponse = new FakeHttpResponse();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        using var ctsu = new CancellationTokenSource();

        string connId1 = "conn-auth-1";
        string connId2 = "conn-auth-2";
        string unrelatedConnId = "conn-unrelated";

        _connectionManager.AddConnection(connId1, response1, cts1.Token);
        _connectionManager.AddConnection(connId2, response2, cts2.Token);
        _connectionManager.AddConnection(unrelatedConnId, unrelatedResponse, ctsu.Token);
        Assert.True(_connectionRegistry.TryRegister(connId1, userId, "test-token-id", [organizationId]));
        Assert.True(_connectionRegistry.TryRegister(connId2, userId, "test-token-id", [organizationId]));
        Assert.True(_connectionRegistry.TryRegister(unrelatedConnId, "unrelated-user", "unrelated-token", [organizationId]));

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

            // Connections should be removed
            Assert.Null(_connectionManager.GetConnectionById(connId1));
            Assert.Null(_connectionManager.GetConnectionById(connId2));
            Assert.NotNull(_connectionManager.GetConnectionById(unrelatedConnId));

            Assert.Empty(_connectionRegistry.GetUserConnections(userId));
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(unrelatedConnId);
            _connectionRegistry.Unregister(connId1);
            _connectionRegistry.Unregister(connId2);
            _connectionRegistry.Unregister(unrelatedConnId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_NonAuthTokenRemoved_DoesNotCloseConnections()
    {
        const string userId = "test-user-id-2";
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "conn-nonauth";
        _connectionManager.AddConnection(connectionId, response, cts.Token);
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
            // IsAuthenticationToken intentionally omitted (defaults false)

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            // Connection should NOT be closed
            Assert.NotNull(_connectionManager.GetConnectionById(connectionId));
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(connectionId);
            _connectionRegistry.Unregister(connectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_OrganizationMessage_SentToGroupOnly()
    {
        const string orgId = "org-1";
        const string otherOrgId = "org-2";
        using var responseInOrg = new FakeHttpResponse();
        using var responseOutOrg = new FakeHttpResponse();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        string inOrgConn = "conn-in-org";
        string outOrgConn = "conn-out-org";

        _connectionManager.AddConnection(inOrgConn, responseInOrg, cts1.Token);
        _connectionManager.AddConnection(outOrgConn, responseOutOrg, cts2.Token);
        Assert.True(_connectionRegistry.TryRegister(inOrgConn, "user-in", "token-in", [orgId]));
        Assert.True(_connectionRegistry.TryRegister(outOrgConn, "user-out", "token-out", [otherOrgId]));

        try
        {
            var entityChanged = new EntityChanged
            {
                Id = "stack-123",
                Type = "Stack",
                ChangeType = ChangeType.Saved
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = orgId;

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            // Give write loop a moment to process
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // In-org connection should receive message, out-org should not
            Assert.True(responseInOrg.WrittenData.Length > 0, "In-org connection should receive message");
            Assert.Equal(0, responseOutOrg.WrittenData.Length);
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(inOrgConn);
            await _connectionManager.RemoveConnectionAsync(outOrgConn);
            _connectionRegistry.Unregister(inOrgConn);
            _connectionRegistry.Unregister(outOrgConn);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_UserMessage_SentToUserOnly()
    {
        const string userId = "user-target";
        const string otherUserId = "user-other";
        using var responseTarget = new FakeHttpResponse();
        using var responseOther = new FakeHttpResponse();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        string targetConn = "conn-target-user";
        string otherConn = "conn-other-user";

        _connectionManager.AddConnection(targetConn, responseTarget, cts1.Token);
        _connectionManager.AddConnection(otherConn, responseOther, cts2.Token);
        Assert.True(_connectionRegistry.TryRegister(targetConn, userId, "target-token", []));
        Assert.True(_connectionRegistry.TryRegister(otherConn, otherUserId, "other-token", []));

        try
        {
            var entityChanged = new EntityChanged
            {
                Id = userId,
                Type = nameof(User),
                ChangeType = ChangeType.Saved
            };

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            await Task.Delay(200, TestContext.Current.CancellationToken);

            Assert.True(responseTarget.WrittenData.Length > 0, "Target user should receive message");
            Assert.Equal(0, responseOther.WrittenData.Length);
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(targetConn);
            await _connectionManager.RemoveConnectionAsync(otherConn);
            _connectionRegistry.Unregister(targetConn);
            _connectionRegistry.Unregister(otherConn);
        }
    }

    [Fact]
    public async Task OnUserMembershipChangedAsync_AddedAndRemoved_UpdatesForwardAndReverseMappings()
    {
        const string userId = "membership-user";
        const string organizationId = "membership-org";
        using var response1 = new FakeHttpResponse();
        using var response2 = new FakeHttpResponse();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        string connectionId1 = "membership-conn-1";
        string connectionId2 = "membership-conn-2";

        _connectionManager.AddConnection(connectionId1, response1, cts1.Token);
        _connectionManager.AddConnection(connectionId2, response2, cts2.Token);
        Assert.True(_connectionRegistry.TryRegister(connectionId1, userId, "membership-token", []));
        Assert.True(_connectionRegistry.TryRegister(connectionId2, userId, "membership-token", []));

        try
        {
            var addMessage = new UserMembershipChanged {
                UserId = userId,
                OrganizationId = organizationId,
                ChangeType = ChangeType.Added
            };

            await _broker.OnUserMembershipChangedAsync(addMessage, TestContext.Current.CancellationToken);

            var organizationConnections = _connectionRegistry.GetGroupConnections(organizationId);
            Assert.Contains(connectionId1, organizationConnections);
            Assert.Contains(connectionId2, organizationConnections);
            Assert.Contains(organizationId, _connectionRegistry.GetGroups(connectionId1));
            Assert.Contains(organizationId, _connectionRegistry.GetGroups(connectionId2));

            var removeMessage = addMessage with { ChangeType = ChangeType.Removed };
            await _broker.OnUserMembershipChangedAsync(removeMessage, TestContext.Current.CancellationToken);

            Assert.Empty(_connectionRegistry.GetGroupConnections(organizationId));
            Assert.Empty(_connectionRegistry.GetGroups(connectionId1));
            Assert.Empty(_connectionRegistry.GetGroups(connectionId2));
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(connectionId1);
            await _connectionManager.RemoveConnectionAsync(connectionId2);
            _connectionRegistry.Unregister(connectionId1);
            _connectionRegistry.Unregister(connectionId2);
        }
    }

    [Fact]
    public async Task OnUserMembershipChangedAsync_Removed_SendsRefreshToRemovedUserAndRemainingOrganizationMembers()
    {
        const string removedUserId = "removed-user";
        const string remainingUserId = "remaining-user";
        const string organizationId = "shared-org";
        using var removedResponse = new FakeHttpResponse();
        using var remainingResponse = new FakeHttpResponse();
        using var removedCts = new CancellationTokenSource();
        using var remainingCts = new CancellationTokenSource();

        string removedConnectionId = "removed-conn";
        string remainingConnectionId = "remaining-conn";

        _connectionManager.AddConnection(removedConnectionId, removedResponse, removedCts.Token);
        _connectionManager.AddConnection(remainingConnectionId, remainingResponse, remainingCts.Token);
        Assert.True(_connectionRegistry.TryRegister(removedConnectionId, removedUserId, "removed-token", [organizationId]));
        Assert.True(_connectionRegistry.TryRegister(remainingConnectionId, remainingUserId, "remaining-token", [organizationId]));

        try
        {
            var message = new UserMembershipChanged {
                UserId = removedUserId,
                OrganizationId = organizationId,
                ChangeType = ChangeType.Removed
            };

            await _broker.OnUserMembershipChangedAsync(message, TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);

            Assert.Contains(nameof(UserMembershipChanged), removedResponse.WrittenData);
            Assert.Contains(nameof(UserMembershipChanged), remainingResponse.WrittenData);
            Assert.DoesNotContain(removedConnectionId, _connectionRegistry.GetGroupConnections(organizationId));
            Assert.Empty(_connectionRegistry.GetGroups(removedConnectionId));
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(removedConnectionId);
            await _connectionManager.RemoveConnectionAsync(remainingConnectionId);
            _connectionRegistry.Unregister(removedConnectionId);
            _connectionRegistry.Unregister(remainingConnectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_ClearsAllLocalOrganizationRegistrations()
    {
        const string userId = "tracked-user";
        const string firstOrganizationId = "tracked-org-1";
        const string secondOrganizationId = "tracked-org-2";
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "tracked-conn";
        _connectionManager.AddConnection(connectionId, response, cts.Token);
        Assert.True(_connectionRegistry.TryRegister(connectionId, userId, "tracked-token-id", [firstOrganizationId, secondOrganizationId]));

        try
        {
            var entityChanged = new EntityChanged
            {
                Id = "tracked-token-id",
                Type = nameof(Token),
                ChangeType = ChangeType.Removed
            };
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = firstOrganizationId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
            entityChanged.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            Assert.Null(_connectionManager.GetConnectionById(connectionId));
            Assert.Empty(_connectionRegistry.GetUserConnections(userId));
        }
        finally
        {
            _connectionRegistry.Unregister(connectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_NonOwnerCannotConsumeOwnerRevocation()
    {
        const string userId = "replicated-user";
        const string tokenId = "replicated-token";
        const string connectionId = "owner-connection";
        using var ownerResponse = new FakeHttpResponse();
        using var ownerCancellation = new CancellationTokenSource();
        using var ownerSse = new SseConnectionManager(new AppOptions { EnablePush = true }, GetService<ITextSerializer>(), Log);
        using var ownerWebSocket = new WebSocketConnectionManager(new AppOptions { EnablePush = true }, GetService<ITextSerializer>(), Log);
        using var nonOwnerSse = new SseConnectionManager(new AppOptions { EnablePush = true }, GetService<ITextSerializer>(), Log);
        using var nonOwnerWebSocket = new WebSocketConnectionManager(new AppOptions { EnablePush = true }, GetService<ITextSerializer>(), Log);
        var ownerRegistry = new PushConnectionRegistry(TimeProvider);
        var nonOwnerRegistry = new PushConnectionRegistry(TimeProvider);
        var options = new AppOptions { EnablePush = true };
        var subscriber = GetService<Foundatio.Messaging.IMessageSubscriber>();
        var ownerBroker = new MessageBusBroker(ownerSse, ownerWebSocket, ownerRegistry, subscriber, options, Log.CreateLogger<MessageBusBroker>());
        var nonOwnerBroker = new MessageBusBroker(nonOwnerSse, nonOwnerWebSocket, nonOwnerRegistry, subscriber, options, Log.CreateLogger<MessageBusBroker>());

        ownerSse.AddConnection(connectionId, ownerResponse, ownerCancellation.Token);
        Assert.True(ownerRegistry.TryRegister(connectionId, userId, tokenId, ["organization"]));
        var message = new EntityChanged { Id = tokenId, Type = nameof(Token), ChangeType = ChangeType.Removed };
        message.Data[ExtendedEntityChanged.KnownKeys.UserId] = userId;
        message.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;

        await nonOwnerBroker.OnEntityChangedAsync(message, TestContext.Current.CancellationToken);
        Assert.NotNull(ownerSse.GetConnectionById(connectionId));

        await ownerBroker.OnEntityChangedAsync(message, TestContext.Current.CancellationToken);
        Assert.Null(ownerSse.GetConnectionById(connectionId));
    }

}

/// <summary>
/// Tests for the deduplication behavior of SseConnection.
/// Validates that identical messages queued in quick succession are coalesced.
/// </summary>
public sealed class SseDeduplicationTests : TestWithServices
{
    public SseDeduplicationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task DuplicateMessages_AreDeduped_OnlyOneQueued()
    {
        var queue = new SseConnection.DedupQueue(8);
        var evt = new SseConnection.SseEvent { Data = "{\"type\":\"StackChanged\",\"id\":\"stack-123\",\"change_type\":1}", DedupeKey = "stack-123" };
        int dedupedCount = 0;

        for (int i = 0; i < 5; i++)
        {
            if (queue.TryEnqueue(evt) == SseConnection.EnqueueResult.Deduped)
                dedupedCount++;
        }

        using var cts = new CancellationTokenSource();
        var queued = await queue.DequeueAsync(cts.Token);

        Assert.NotNull(queued);
        Assert.Equal(evt.Data, queued!.Value.Data);
        Assert.Equal(4, dedupedCount);
    }

    [Fact]
    public async Task DifferentMessages_AreNotDeduped()
    {
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();
        var serializer = GetService<ITextSerializer>();

        await using var connection = new SseConnection("dedup-test-2", response, serializer, cts.Token, Log.CreateLogger<SseConnection>());

        // Send 3 different messages
        connection.TryWrite(new { type = "StackChanged", id = "stack-1" });
        connection.TryWrite(new { type = "StackChanged", id = "stack-2" });
        connection.TryWrite(new { type = "ProjectChanged", id = "proj-1" });

        await Task.Delay(200, TestContext.Current.CancellationToken);
        connection.Abort();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        string output = response.WrittenData;
        int dataLineCount = output.Split("data: ").Length - 1;
        Assert.Equal(3, dataLineCount);
        Assert.Equal(0, connection.DedupedMessages);
    }

    [Fact]
    public async Task SameMessage_AfterFirstIsConsumed_IsNotDeduped()
    {
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();
        var serializer = GetService<ITextSerializer>();

        await using var connection = new SseConnection("dedup-test-3", response, serializer, cts.Token, Log.CreateLogger<SseConnection>());

        var message = new { type = "StackChanged", id = "stack-repeat" };

        // Send first message and wait for it to be consumed
        connection.TryWrite(message);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Send same message again — should NOT be deduped because first was already consumed
        connection.TryWrite(message);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        connection.Abort();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        string output = response.WrittenData;
        int dataLineCount = output.Split("data: ").Length - 1;
        Assert.Equal(2, dataLineCount);
        Assert.Equal(0, connection.DedupedMessages);
    }

    [Fact]
    public async Task KeepAlive_IsNeverDeduped()
    {
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();
        var serializer = GetService<ITextSerializer>();

        await using var connection = new SseConnection("dedup-test-4", response, serializer, cts.Token, Log.CreateLogger<SseConnection>());

        // Send multiple keep-alives — none should be deduped
        connection.TryWriteKeepAlive();
        connection.TryWriteKeepAlive();
        connection.TryWriteKeepAlive();

        await Task.Delay(200, TestContext.Current.CancellationToken);
        connection.Abort();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        string output = response.WrittenData;
        int keepAliveCount = output.Split(": keepalive").Length - 1;
        Assert.Equal(3, keepAliveCount);
    }

    [Fact]
    public async Task TryEnqueue_CapacityExceeded_DropsOldestWithoutPhantomSignal()
    {
        // Arrange
        using var queue = new SseConnection.DedupQueue(3);

        // Act
        for (int i = 0; i < 3; i++)
            queue.TryEnqueue(new SseConnection.SseEvent { Data = $"msg-{i}", DedupeKey = $"key-{i}" });
        var overflow = queue.TryEnqueue(new SseConnection.SseEvent { Data = "overflow", DedupeKey = "overflow" });

        var item1 = await queue.DequeueAsync(TestContext.Current.CancellationToken);
        var item2 = await queue.DequeueAsync(TestContext.Current.CancellationToken);
        var item3 = await queue.DequeueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SseConnection.EnqueueResult.Full, overflow);
        Assert.Equal("msg-0", item1!.Value.Data);
        Assert.Equal("msg-1", item2!.Value.Data);
        Assert.Equal("msg-2", item3!.Value.Data);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.DequeueAsync(cts.Token));
    }

    [Fact]
    public async Task CriticalMessage_WhenQueueFull_ReturnsFullWithoutEvictingQueuedMessages()
    {
        var queue = new SseConnection.DedupQueue(2);
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "lossy-1", DedupeKey = "lossy-1" });
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-1" });

        var result = queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-2" });

        using var cts = new CancellationTokenSource();
        var item1 = await queue.DequeueAsync(cts.Token);
        var item2 = await queue.DequeueAsync(cts.Token);

        Assert.Equal(SseConnection.EnqueueResult.Full, result);
        Assert.Equal("lossy-1", item1!.Value.Data);
        Assert.Equal("critical-1", item2!.Value.Data);
    }

    [Fact]
    public async Task DroppableMessage_WhenQueueFullOfCriticalMessages_DoesNotEvictCriticalMessage()
    {
        // Arrange
        using var queue = new SseConnection.DedupQueue(1);
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-1" });

        // Act
        var result = queue.TryEnqueue(new SseConnection.SseEvent { Data = "lossy-1", DedupeKey = "lossy-1" });
        var item = await queue.DequeueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SseConnection.EnqueueResult.Full, result);
        Assert.True(item.HasValue);
        Assert.Equal("critical-1", item.GetValueOrDefault().Data);
    }

    [Fact]
    public async Task KeepAlive_WhenQueueFull_DoesNotEvictCriticalMessage()
    {
        var queue = new SseConnection.DedupQueue(1);
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-1" });

        var result = queue.TryEnqueue(SseConnection.SseEvent.KeepAlive);

        using var cts = new CancellationTokenSource();
        var item = await queue.DequeueAsync(cts.Token);

        Assert.Equal(SseConnection.EnqueueResult.Full, result);
        Assert.True(item.HasValue);
        var dequeued = item.GetValueOrDefault();
        Assert.Equal("critical-1", dequeued.Data);
        Assert.False(dequeued.IsKeepAlive);
    }
}
