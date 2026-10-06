using Exceptionless.Web.Hubs;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class PushConnectionRegistryTests
{
    [Fact]
    public void TryRegister_RevocationArrivesFirst_RejectsInFlightConnection()
    {
        var registry = new PushConnectionRegistry(new ProxyTimeProvider());

        Assert.Empty(registry.RevokeToken("token"));
        Assert.False(registry.TryRegister("connection", "user", "token", ["organization"]));
    }

    [Fact]
    public void RevokeToken_RegisteredConnection_ReturnsOnlyLocalTokenConnections()
    {
        var registry = new PushConnectionRegistry(new ProxyTimeProvider());
        Assert.True(registry.TryRegister("target", "user", "token", ["organization"]));
        Assert.True(registry.TryRegister("other", "user", "other-token", ["organization"]));

        Assert.Equal(["target"], registry.RevokeToken("token"));
    }

    [Fact]
    public void MembershipAndUnregister_KeepRoutingIndexesConsistent()
    {
        var registry = new PushConnectionRegistry(new ProxyTimeProvider());
        Assert.True(registry.TryRegister("connection", "user", "token", ["first"]));

        registry.AddGroup("connection", "second");
        registry.RemoveGroup("connection", "first");

        Assert.Equal(["connection"], registry.GetUserConnections("user"));
        Assert.Empty(registry.GetGroupConnections("first"));
        Assert.Equal(["connection"], registry.GetGroupConnections("second"));

        registry.Unregister("connection");

        Assert.Empty(registry.GetUserConnections("user"));
        Assert.Empty(registry.GetGroupConnections("second"));
        Assert.Empty(registry.RevokeToken("token"));
    }
}
