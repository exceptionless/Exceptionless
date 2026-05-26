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
    }

    [Fact]
    public async Task RemoveAsync_LastConnection_ReturnsEmptyCollection()
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
    }

    [Fact]
    public Task RemoveAsync_UnknownKey_DoesNotThrow()
    {
        // Arrange
        var mapping = new ConnectionMapping();

        // Act & Assert
        return mapping.RemoveAsync("nonexistent", "conn1");
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
        await mapping.GroupAddAsync("org1", "conn1");
        await mapping.GroupAddAsync("org1", "conn2");

        // Act
        await mapping.GroupRemoveAsync("org1", "conn1");

        // Assert
        var connections = await mapping.GetGroupConnectionsAsync("org1");
        Assert.DoesNotContain("conn1", connections);
        Assert.Contains("conn2", connections);
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
