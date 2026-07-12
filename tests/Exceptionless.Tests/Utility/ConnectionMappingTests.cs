using Exceptionless.Core.Utility;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class ConnectionMappingTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public async Task AddAsync_NewKey_CanRetrieveConnection()
    {
        // Arrange
        var mapping = new ConnectionMapping();

        // Act
        await mapping.AddAsync("user1", "conn1");

        // Assert
        var connections = await mapping.GetConnectionsAsync("user1");
        Assert.Contains("conn1", connections);
        Assert.Equal(1, await mapping.GetConnectionCountAsync("user1"));
    }

    [Fact]
    public async Task AddAsync_ExistingKey_AccumulatesConnections()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.AddAsync("user1", "conn1");

        // Act
        await mapping.AddAsync("user1", "conn2");

        // Assert
        var connections = await mapping.GetConnectionsAsync("user1");
        Assert.Equal(2, connections.Count);
        Assert.Contains("conn1", connections);
        Assert.Contains("conn2", connections);
        Assert.Equal(1, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task AddAsync_NullKey_DoesNotTrackConnection()
    {
        // Arrange
        var mapping = new ConnectionMapping();

        // Act
        await mapping.AddAsync(null!, "conn1");

        // Assert
        Assert.Empty(await mapping.GetConnectionsAsync(null!));
        Assert.Equal(0, await mapping.GetConnectionCountAsync(null!));
        Assert.Equal(0, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task RemoveAsync_LastConnection_RemovesTrackedKey()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.AddAsync("user1", "conn1");

        // Act
        await mapping.RemoveAsync("user1", "conn1");

        // Assert
        var connections = await mapping.GetConnectionsAsync("user1");
        Assert.Empty(connections);
        Assert.Equal(0, await mapping.GetConnectionCountAsync("user1"));
        Assert.Equal(0, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task RemoveAsync_OneOfMultipleConnections_LeavesRemainder()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.AddAsync("user1", "conn1");
        await mapping.AddAsync("user1", "conn2");

        // Act
        await mapping.RemoveAsync("user1", "conn1");

        // Assert
        var connections = await mapping.GetConnectionsAsync("user1");
        Assert.DoesNotContain("conn1", connections);
        Assert.Contains("conn2", connections);
        Assert.Single(connections);
        Assert.Equal(1, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task RemoveAsync_UnknownKey_DoesNotTrackEmptyKey()
    {
        // Arrange
        var mapping = new ConnectionMapping();

        // Act
        await mapping.RemoveAsync("nonexistent", "conn1");

        // Assert
        Assert.Empty(await mapping.GetConnectionsAsync("nonexistent"));
        Assert.Equal(0, await mapping.GetConnectionCountAsync("nonexistent"));
        Assert.Equal(0, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task RemoveAsync_UnknownConnection_DoesNotAffectOthers()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.AddAsync("user1", "conn1");

        // Act
        await mapping.RemoveAsync("user1", "conn-missing");

        // Assert
        var connections = await mapping.GetConnectionsAsync("user1");
        Assert.Contains("conn1", connections);
        Assert.Single(connections);
        Assert.Equal(1, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task RemoveAsync_NullKey_DoesNotTrackEmptyKey()
    {
        // Arrange
        var mapping = new ConnectionMapping();

        // Act
        await mapping.RemoveAsync(null!, "conn1");

        // Assert
        Assert.Empty(await mapping.GetConnectionsAsync(null!));
        Assert.Equal(0, await mapping.GetConnectionCountAsync(null!));
        Assert.Equal(0, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task AddAsync_AfterLastConnectionRemoved_RecreatesTrackedKey()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.AddAsync("user1", "conn1");
        await mapping.RemoveAsync("user1", "conn1");

        // Act
        await mapping.AddAsync("user1", "conn2");

        // Assert
        var connections = await mapping.GetConnectionsAsync("user1");
        Assert.DoesNotContain("conn1", connections);
        Assert.Contains("conn2", connections);
        Assert.Single(connections);
        Assert.Equal(1, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task AddAsync_ConcurrentWithRemovingLastConnection_PreservesAddedConnection()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        const string key = "user1";
        await mapping.AddAsync(key, "conn1");

        // Act
        await Task.WhenAll(
            Task.Run(() => mapping.RemoveAsync(key, "conn1"), TestContext.Current.CancellationToken),
            Task.Run(() => mapping.AddAsync(key, "conn2"), TestContext.Current.CancellationToken));

        // Assert
        var connections = await mapping.GetConnectionsAsync(key);
        Assert.DoesNotContain("conn1", connections);
        Assert.Contains("conn2", connections);
        Assert.Single(connections);
        Assert.Equal(1, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task RemoveAsync_AllConnectionsRemovedConcurrently_RemovesTrackedKey()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        const string key = "user1";
        await mapping.AddAsync(key, "conn1");
        await mapping.AddAsync(key, "conn2");

        // Act
        await Task.WhenAll(
            Task.Run(() => mapping.RemoveAsync(key, "conn1"), TestContext.Current.CancellationToken),
            Task.Run(() => mapping.RemoveAsync(key, "conn2"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(await mapping.GetConnectionsAsync(key));
        Assert.Equal(0, await mapping.GetConnectionCountAsync(key));
        Assert.Equal(0, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task AddAsyncAndRemoveAsync_ConcurrentSameKey_CleansTrackedKey()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        const string key = "user1";
        string[] connectionIds = Enumerable.Range(0, 512).Select(i => $"conn{i}").ToArray();

        // Act
        await Task.WhenAll(connectionIds.Select(connectionId => Task.Run(async () =>
        {
            await mapping.AddAsync(key, connectionId);
            await Task.Yield();
            await mapping.RemoveAsync(key, connectionId);
        })));

        // Assert
        Assert.Empty(await mapping.GetConnectionsAsync(key));
        Assert.Equal(0, await mapping.GetConnectionCountAsync(key));
        Assert.Equal(0, mapping.TrackedKeyCount);
    }

    [Fact]
    public async Task GetConnectionsAsync_NullKey_ReturnsEmpty()
    {
        // Arrange
        var mapping = new ConnectionMapping();

        // Act
        var connections = await mapping.GetConnectionsAsync(null!);

        // Assert
        Assert.Empty(connections);
    }

    [Fact]
    public async Task GetConnectionsAsync_ReturnsSnapshot_NotLiveReference()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.AddAsync("user1", "conn1");

        // Act
        var snapshot = await mapping.GetConnectionsAsync("user1");
        await mapping.AddAsync("user1", "conn2");

        // Assert – snapshot is not affected by later mutations
        Assert.Single(snapshot);
        Assert.Equal(2, (await mapping.GetConnectionsAsync("user1")).Count);
    }

    [Fact]
    public async Task GroupExtensions_AddAndRemove_WorkCorrectly()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.GroupAddAsync("organization1", "conn1");
        await mapping.GroupAddAsync("organization1", "conn2");

        // Act
        await mapping.GroupRemoveAsync("organization1", "conn1");

        // Assert
        var connections = await mapping.GetGroupConnectionsAsync("organization1");
        Assert.DoesNotContain("conn1", connections);
        Assert.Contains("conn2", connections);
        Assert.Equal(1, await mapping.GetGroupConnectionCountAsync("organization1"));
    }

    [Fact]
    public async Task UserIdExtensions_AddAndRemove_WorkCorrectly()
    {
        // Arrange
        var mapping = new ConnectionMapping();
        await mapping.UserIdAddAsync("user1", "conn1");

        // Act
        await mapping.UserIdRemoveAsync("user1", "conn1");

        // Assert
        var connections = await mapping.GetUserIdConnectionsAsync("user1");
        Assert.Empty(connections);
    }
}
