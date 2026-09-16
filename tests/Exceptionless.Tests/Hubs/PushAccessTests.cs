using System.Security.Claims;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Exceptionless.Web.Hubs;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class PushAccessTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData(ChangeType.Removed, false)]
    [InlineData(ChangeType.Saved, true)]
    public async Task OAuthTokenRevoked_ClosesBothTransportsWithoutClosingOtherTokens(ChangeType changeType, bool revoked)
    {
        var sse = GetService<SseConnectionManager>();
        var webSockets = GetService<WebSocketConnectionManager>();
        var registry = GetService<PushConnectionRegistry>();
        using var response = new FakeHttpResponse();
        using var socket = new TestWebSocket();
        using var otherSocket = new TestWebSocket();
        sse.AddConnection("sse", response, TestContext.Current.CancellationToken);
        webSockets.AddConnection("websocket", socket);
        webSockets.AddConnection("other", otherSocket);
        Assert.True(registry.TryRegister("sse", "user", "revoked-token", ["organization"]));
        Assert.True(registry.TryRegister("websocket", "user", "revoked-token", ["organization"]));
        Assert.True(registry.TryRegister("other", "user", "other-token", ["organization"]));
        var message = new EntityChanged { Type = nameof(OAuthToken), Id = "revoked-token", ChangeType = changeType };
        message.Data[ExtendedEntityChanged.KnownKeys.IsTokenRevoked] = revoked;

        try
        {
            await GetService<MessageBusBroker>().OnEntityChangedAsync(message, TestContext.Current.CancellationToken);

            Assert.Null(sse.GetConnectionById("sse"));
            Assert.Null(webSockets.GetConnectionById("websocket"));
            Assert.Same(otherSocket, webSockets.GetConnectionById("other"));
            Assert.Equal(["other"], registry.GetGroupConnections("organization"));
            Assert.False(registry.TryRegister("late", "user", "revoked-token", ["organization"]));
        }
        finally
        {
            await sse.RemoveConnectionAsync("sse");
            await webSockets.RemoveConnectionAsync("websocket");
            await webSockets.RemoveConnectionAsync("other");
        }
    }

    [Fact]
    public void OAuthPrincipal_TracksUserForMembershipRemoval()
    {
        var user = new User { Id = "user", EmailAddress = "user@example.test", OrganizationIds = new HashSet<string> { "allowed", "other" } };
        var token = new OAuthToken { Id = "token", UserId = user.Id, ClientId = "client", Resource = "https://localhost/api/v2", OrganizationIds = ["allowed"] };

        Assert.True(PushPrincipal.TryCreate(new ClaimsPrincipal(user.ToIdentity(token)), out var principal));

        Assert.Equal(user.Id, principal.UserId);
        Assert.Equal(["allowed"], principal.OrganizationIds);
        Assert.False(principal.FollowMembershipAdditions);
    }

    [Fact]
    public async Task ScopedMembership_RemovesAccessWithoutExpandingGrantedOrganizations()
    {
        var registry = GetService<PushConnectionRegistry>();
        Assert.True(registry.TryRegister("scoped", "user", "token", ["allowed"], followMembershipAdditions: false));
        Assert.True(registry.TryRegister("session", "user", "session-token", ["allowed"]));
        var broker = GetService<MessageBusBroker>();

        await broker.OnUserMembershipChangedAsync(new UserMembershipChanged { UserId = "user", OrganizationId = "other", ChangeType = ChangeType.Added }, TestContext.Current.CancellationToken);
        Assert.Equal(["allowed"], registry.GetGroups("scoped"));
        Assert.Contains("other", registry.GetGroups("session"));

        await broker.OnUserMembershipChangedAsync(new UserMembershipChanged { UserId = "user", OrganizationId = "allowed", ChangeType = ChangeType.Removed }, TestContext.Current.CancellationToken);
        Assert.Empty(registry.GetGroups("scoped"));
        Assert.Equal(["other"], registry.GetGroups("session"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task OAuthTokenSaved_PublishesRevocationState(bool disabled, bool suspended, bool expectedRevoked)
    {
        var received = new TaskCompletionSource<EntityChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        await GetService<IMessageSubscriber>().SubscribeAsync<EntityChanged>(message => received.TrySetResult(message), TestContext.Current.CancellationToken);
        var repository = new PublishingOAuthTokenRepository(GetService<ExceptionlessElasticConfiguration>(), GetService<MiniValidationValidator>(), GetService<AppOptions>());

        await repository.PublishAsync(new OAuthToken { Id = "token", IsDisabled = disabled, IsSuspended = suspended });
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(nameof(OAuthToken), message.Type);
        Assert.Equal("token", message.Id);
        Assert.Equal(expectedRevoked, message.Data[ExtendedEntityChanged.KnownKeys.IsTokenRevoked]);
    }

    private sealed class PublishingOAuthTokenRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : OAuthTokenRepository(configuration, validator, options)
    {
        public Task PublishAsync(OAuthToken token) => PublishChangeTypeMessageAsync(ChangeType.Saved, token);
    }
}
