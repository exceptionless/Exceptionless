using System.Security.Claims;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Hubs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class PushDisconnectCleanupTests
{
    [Fact]
    public async Task GetOrganizationIdsAsync_UserAddedToOrganizationAfterConnect_IncludesCurrentMemberships()
    {
        // Arrange
        var user = CreateUser("user1", "org-a");
        ClaimsPrincipal principal = new(user.ToIdentity());
        var connectionMapping = new ConnectionMapping();
        const string connectionId = "push-connection";
        await connectionMapping.ConnectionGroupAddAsync(connectionId, "org-a");

        var currentUser = CreateUser("user1", "org-a", "org-b");

        // Act
        var organizationIds = await PushDisconnectCleanup.GetOrganizationIdsAsync(principal, connectionId, connectionMapping, () => Task.FromResult<User?>(currentUser), NullLogger.Instance);

        // Assert
        Assert.Contains("org-a", organizationIds);
        Assert.Contains("org-b", organizationIds);
    }

    [Fact]
    public async Task GetOrganizationIdsAsync_UserAddedToOrganizationAfterConnect_CleansUpAddedOrganizationMapping()
    {
        // Arrange
        var user = CreateUser("user1", "org-a");
        ClaimsPrincipal principal = new(user.ToIdentity());
        var connectionMapping = new ConnectionMapping();
        const string connectionId = "push-connection";
        await connectionMapping.GroupAddAsync("org-a", connectionId);
        await connectionMapping.ConnectionGroupAddAsync(connectionId, "org-a");
        await connectionMapping.GroupAddAsync("org-b", connectionId);
        await connectionMapping.ConnectionGroupAddAsync(connectionId, "org-b");
        await connectionMapping.UserIdAddAsync(user.Id, connectionId);

        var currentUser = CreateUser("user1", "org-a", "org-b");

        // Act
        foreach (string organizationId in await PushDisconnectCleanup.GetOrganizationIdsAsync(principal, connectionId, connectionMapping, () => Task.FromResult<User?>(currentUser), NullLogger.Instance))
        {
            await connectionMapping.GroupRemoveAsync(organizationId, connectionId);
            await connectionMapping.ConnectionGroupRemoveAsync(connectionId, organizationId);
        }

        await connectionMapping.UserIdRemoveAsync(user.Id, connectionId);

        // Assert
        Assert.DoesNotContain(connectionId, await connectionMapping.GetGroupConnectionsAsync("org-a"));
        Assert.DoesNotContain(connectionId, await connectionMapping.GetGroupConnectionsAsync("org-b"));
        Assert.Empty(await connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.DoesNotContain(connectionId, await connectionMapping.GetUserIdConnectionsAsync(user.Id));
    }

    [Fact]
    public async Task GetOrganizationIdsAsync_WhenRepositoryLookupFails_FallsBackToTrackedConnectionGroups()
    {
        // Arrange
        var user = CreateUser("user1", "org-a");
        ClaimsPrincipal principal = new(user.ToIdentity());
        var connectionMapping = new ConnectionMapping();
        const string connectionId = "push-connection";
        await connectionMapping.GroupAddAsync("org-a", connectionId);
        await connectionMapping.ConnectionGroupAddAsync(connectionId, "org-a");
        await connectionMapping.GroupAddAsync("org-b", connectionId);
        await connectionMapping.ConnectionGroupAddAsync(connectionId, "org-b");
        await connectionMapping.UserIdAddAsync(user.Id, connectionId);

        // Act
        foreach (string organizationId in await PushDisconnectCleanup.GetOrganizationIdsAsync(principal, connectionId, connectionMapping, () => throw new InvalidOperationException("boom"), NullLogger.Instance))
        {
            await connectionMapping.GroupRemoveAsync(organizationId, connectionId);
            await connectionMapping.ConnectionGroupRemoveAsync(connectionId, organizationId);
        }

        await connectionMapping.UserIdRemoveAsync(user.Id, connectionId);

        // Assert
        Assert.DoesNotContain(connectionId, await connectionMapping.GetGroupConnectionsAsync("org-a"));
        Assert.DoesNotContain(connectionId, await connectionMapping.GetGroupConnectionsAsync("org-b"));
        Assert.Empty(await connectionMapping.GetConnectionGroupsAsync(connectionId));
        Assert.DoesNotContain(connectionId, await connectionMapping.GetUserIdConnectionsAsync(user.Id));
    }

    private static User CreateUser(string userId, params string[] organizationIds)
    {
        var user = new User {
            Id = userId,
            EmailAddress = $"{userId}@example.com"
        };

        foreach (string organizationId in organizationIds)
            user.OrganizationIds.Add(organizationId);

        return user;
    }
}
