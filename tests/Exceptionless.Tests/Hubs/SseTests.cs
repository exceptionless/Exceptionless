using Exceptionless.Core;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
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

public sealed class PushMiddlewareMappingTests : TestWithServices
{
    private readonly IConnectionMapping _connectionMapping;

    public PushMiddlewareMappingTests(ITestOutputHelper output) : base(output)
    {
        _connectionMapping = GetService<IConnectionMapping>();
    }

    [Fact]
    public async Task OnConnectedAndDisconnected_SseMiddleware_UpdatesReverseConnectionMappings()
    {
        // Arrange
        const string userId = "middleware-user";
        const string connectionId = "sse-middleware-conn";
        var context = CreateUserContext(userId, "organization-a", "organization-b");
        var middleware = new SseMiddleware(_ => Task.CompletedTask, GetService<SseConnectionManager>(), _connectionMapping, Log.CreateLogger<SseMiddleware>());

        // Act
        await middleware.OnConnected(context, connectionId);

        // Assert
        Assert.Contains(connectionId, await _connectionMapping.GetGroupConnectionsAsync("organization-a"));
        Assert.Contains(connectionId, await _connectionMapping.GetGroupConnectionsAsync("organization-b"));
        Assert.Contains("organization-a", await _connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.Contains("organization-b", await _connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.Contains(connectionId, await _connectionMapping.GetUserIdConnectionsAsync(userId));

        // Act
        await middleware.OnDisconnected(context, connectionId);

        // Assert
        Assert.Empty(await _connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.Empty(await _connectionMapping.GetUserIdConnectionsAsync(userId));
        Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync("organization-a"));
        Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync("organization-b"));
    }

    [Fact]
    public async Task OnConnectedAndDisconnected_WebSocketPushMiddleware_UpdatesReverseConnectionMappings()
    {
        // Arrange
        const string userId = "ws-middleware-user";
        const string connectionId = "ws-middleware-conn";
        var context = CreateUserContext(userId, "organization-a", "organization-b");
        var middleware = new WebSocketPushMiddleware(_ => Task.CompletedTask, GetService<WebSocketConnectionManager>(), _connectionMapping, Log.CreateLogger<WebSocketPushMiddleware>());

        // Act
        await middleware.OnConnected(context, connectionId);

        // Assert
        Assert.Contains(connectionId, await _connectionMapping.GetGroupConnectionsAsync("organization-a"));
        Assert.Contains(connectionId, await _connectionMapping.GetGroupConnectionsAsync("organization-b"));
        Assert.Contains("organization-a", await _connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.Contains("organization-b", await _connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.Contains(connectionId, await _connectionMapping.GetUserIdConnectionsAsync(userId));

        // Act
        await middleware.OnDisconnected(context, connectionId);

        // Assert
        Assert.Empty(await _connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.Empty(await _connectionMapping.GetUserIdConnectionsAsync(userId));
        Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync("organization-a"));
        Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync("organization-b"));
    }

    private static DefaultHttpContext CreateUserContext(string userId, params string[] organizationIds)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(Exceptionless.Core.Extensions.IdentityUtils.OrganizationIdsClaim, String.Join(",", organizationIds))
        ], Exceptionless.Core.Extensions.IdentityUtils.UserAuthenticationType));

        return context;
    }

}

/// <summary>
/// Tests for MessageBusBroker using SSE connections.
/// </summary>
public sealed class SseBrokerTests : TestWithServices
{
    private readonly MessageBusBroker _broker;
    private readonly IConnectionMapping _connectionMapping;
    private readonly SseConnectionManager _connectionManager;

    public SseBrokerTests(ITestOutputHelper output) : base(output)
    {
        _broker = GetService<MessageBusBroker>();
        _connectionMapping = GetService<IConnectionMapping>();
        _connectionManager = GetService<SseConnectionManager>();
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

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, connId1);
            await _connectionMapping.UserIdAddAsync(userId, connId2);
            await _connectionMapping.GroupAddAsync(organizationId, connId1);
            await _connectionMapping.GroupAddAsync(organizationId, connId2);
            await _connectionMapping.GroupAddAsync(organizationId, unrelatedConnId);

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

            // User mapping cleared
            var remaining = await _connectionMapping.GetUserIdConnectionsAsync(userId);
            Assert.Empty(remaining);

            // Org mapping only has unrelated connection
            var orgConnections = await _connectionMapping.GetGroupConnectionsAsync(organizationId);
            Assert.DoesNotContain(connId1, orgConnections);
            Assert.DoesNotContain(connId2, orgConnections);
            Assert.Contains(unrelatedConnId, orgConnections);
        }
        finally
        {
            await _connectionMapping.GroupRemoveAsync(organizationId, unrelatedConnId);
            await _connectionManager.RemoveConnectionAsync(unrelatedConnId);
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

            await _broker.OnEntityChangedAsync(entityChanged, CancellationToken.None);

            // Connection should NOT be closed
            Assert.NotNull(_connectionManager.GetConnectionById(connectionId));
        }
        finally
        {
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId);
            await _connectionManager.RemoveConnectionAsync(connectionId);
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

        try
        {
            await _connectionMapping.GroupAddAsync(orgId, inOrgConn);
            await _connectionMapping.GroupAddAsync(otherOrgId, outOrgConn);

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
            await _connectionMapping.GroupRemoveAsync(orgId, inOrgConn);
            await _connectionMapping.GroupRemoveAsync(otherOrgId, outOrgConn);
            await _connectionManager.RemoveConnectionAsync(inOrgConn);
            await _connectionManager.RemoveConnectionAsync(outOrgConn);
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

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, targetConn);
            await _connectionMapping.UserIdAddAsync(otherUserId, otherConn);

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
            await _connectionMapping.UserIdRemoveAsync(userId, targetConn);
            await _connectionMapping.UserIdRemoveAsync(otherUserId, otherConn);
            await _connectionManager.RemoveConnectionAsync(targetConn);
            await _connectionManager.RemoveConnectionAsync(otherConn);
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

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, connectionId1);
            await _connectionMapping.UserIdAddAsync(userId, connectionId2);

            var addMessage = new UserMembershipChanged {
                UserId = userId,
                OrganizationId = organizationId,
                ChangeType = ChangeType.Added
            };

            await _broker.OnUserMembershipChangedAsync(addMessage, TestContext.Current.CancellationToken);

            var organizationConnections = await _connectionMapping.GetGroupConnectionsAsync(organizationId);
            Assert.Contains(connectionId1, organizationConnections);
            Assert.Contains(connectionId2, organizationConnections);
            Assert.Contains(organizationId, await _connectionMapping.GetConnectionGroupsAsync(connectionId1));
            Assert.Contains(organizationId, await _connectionMapping.GetConnectionGroupsAsync(connectionId2));

            var removeMessage = addMessage with { ChangeType = ChangeType.Removed };
            await _broker.OnUserMembershipChangedAsync(removeMessage, TestContext.Current.CancellationToken);

            Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync(organizationId));
            Assert.Empty(await _connectionMapping.GetConnectionGroupsAsync(connectionId1));
            Assert.Empty(await _connectionMapping.GetConnectionGroupsAsync(connectionId2));
        }
        finally
        {
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId1);
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId2);
            await _connectionManager.RemoveConnectionAsync(connectionId1);
            await _connectionManager.RemoveConnectionAsync(connectionId2);
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

        try
        {
            await _connectionMapping.UserIdAddAsync(removedUserId, removedConnectionId);
            await _connectionMapping.UserIdAddAsync(remainingUserId, remainingConnectionId);
            await _connectionMapping.GroupAddAsync(organizationId, removedConnectionId);
            await _connectionMapping.GroupAddAsync(organizationId, remainingConnectionId);
            await _connectionMapping.ConnectionGroupAddAsync(removedConnectionId, organizationId);

            var message = new UserMembershipChanged {
                UserId = removedUserId,
                OrganizationId = organizationId,
                ChangeType = ChangeType.Removed
            };

            await _broker.OnUserMembershipChangedAsync(message, TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);

            Assert.Contains(nameof(UserMembershipChanged), removedResponse.WrittenData);
            Assert.Contains(nameof(UserMembershipChanged), remainingResponse.WrittenData);
            Assert.DoesNotContain(removedConnectionId, await _connectionMapping.GetGroupConnectionsAsync(organizationId));
            Assert.Empty(await _connectionMapping.GetConnectionGroupsAsync(removedConnectionId));
        }
        finally
        {
            await _connectionMapping.UserIdRemoveAsync(removedUserId, removedConnectionId);
            await _connectionMapping.UserIdRemoveAsync(remainingUserId, remainingConnectionId);
            await _connectionMapping.GroupRemoveAsync(organizationId, removedConnectionId);
            await _connectionMapping.GroupRemoveAsync(organizationId, remainingConnectionId);
            await _connectionMapping.ConnectionGroupRemoveAsync(removedConnectionId, organizationId);
            await _connectionManager.RemoveConnectionAsync(removedConnectionId);
            await _connectionManager.RemoveConnectionAsync(remainingConnectionId);
        }
    }

    [Fact]
    public async Task OnEntityChangedAsync_AuthTokenRemoved_ClearsAllTrackedOrganizationMappings()
    {
        const string userId = "tracked-user";
        const string firstOrganizationId = "tracked-org-1";
        const string secondOrganizationId = "tracked-org-2";
        using var response = new FakeHttpResponse();
        using var cts = new CancellationTokenSource();

        string connectionId = "tracked-conn";
        _connectionManager.AddConnection(connectionId, response, cts.Token);

        try
        {
            await _connectionMapping.UserIdAddAsync(userId, connectionId);
            await _connectionMapping.GroupAddAsync(firstOrganizationId, connectionId);
            await _connectionMapping.GroupAddAsync(secondOrganizationId, connectionId);
            await _connectionMapping.ConnectionGroupAddAsync(connectionId, firstOrganizationId);
            await _connectionMapping.ConnectionGroupAddAsync(connectionId, secondOrganizationId);

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
            Assert.Empty(await _connectionMapping.GetUserIdConnectionsAsync(userId));
            Assert.Empty(await _connectionMapping.GetConnectionGroupsAsync(connectionId));
            Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync(firstOrganizationId));
            Assert.Empty(await _connectionMapping.GetGroupConnectionsAsync(secondOrganizationId));
        }
        finally
        {
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId);
            await _connectionMapping.GroupRemoveAsync(firstOrganizationId, connectionId);
            await _connectionMapping.GroupRemoveAsync(secondOrganizationId, connectionId);
            await _connectionMapping.ConnectionGroupRemoveAsync(connectionId, firstOrganizationId);
            await _connectionMapping.ConnectionGroupRemoveAsync(connectionId, secondOrganizationId);
        }
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
    public async Task Capacity_Exceeded_DropsOldest()
    {
        // Test the DedupQueue directly to avoid racing with the write loop
        var queue = new SseConnection.DedupQueue(3);

        // Enqueue 5 items with unique keys — first 2 should be dropped
        for (int i = 0; i < 5; i++)
        {
            queue.TryEnqueue(new SseConnection.SseEvent { Data = $"msg-{i}", DedupeKey = $"key-{i}" });
        }

        // Dequeue and verify we get the last 3 items (oldest 2 were dropped)
        using var cts = new CancellationTokenSource();
        var item1 = await queue.DequeueAsync(cts.Token);
        var item2 = await queue.DequeueAsync(cts.Token);
        var item3 = await queue.DequeueAsync(cts.Token);

        Assert.Equal("msg-2", item1!.Value.Data);
        Assert.Equal("msg-3", item2!.Value.Data);
        Assert.Equal("msg-4", item3!.Value.Data);
    }

    [Fact]
    public async Task CriticalMessage_WhenQueueFull_DropsOldestDroppableMessageFirst()
    {
        var queue = new SseConnection.DedupQueue(2);
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "lossy-1", DedupeKey = "lossy-1", CanDrop = true });
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-1", CanDrop = false });

        var result = queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-2", CanDrop = false });

        using var cts = new CancellationTokenSource();
        var item1 = await queue.DequeueAsync(cts.Token);
        var item2 = await queue.DequeueAsync(cts.Token);

        Assert.Equal(SseConnection.EnqueueResult.DroppedQueuedMessage, result);
        Assert.Equal("critical-1", item1!.Value.Data);
        Assert.Equal("critical-2", item2!.Value.Data);
    }

    [Fact]
    public async Task KeepAlive_WhenQueueFull_DoesNotEvictCriticalMessage()
    {
        var queue = new SseConnection.DedupQueue(1);
        queue.TryEnqueue(new SseConnection.SseEvent { Data = "critical-1", CanDrop = false });

        var result = queue.TryEnqueue(SseConnection.SseEvent.KeepAlive);

        using var cts = new CancellationTokenSource();
        var item = await queue.DequeueAsync(cts.Token);

        Assert.Equal(SseConnection.EnqueueResult.Skipped, result);
        Assert.True(item.HasValue);
        var dequeued = item.GetValueOrDefault();
        Assert.Equal("critical-1", dequeued.Data);
        Assert.False(dequeued.IsKeepAlive);
    }
}
