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
    [InlineData(true, ChangeType.Added)]
    [InlineData(true, ChangeType.Saved)]
    [InlineData(false, ChangeType.Added)]
    public async Task OnEntityChangedAsync_TokenCredentials_OnlyNormalSessionsReceiveCanary(bool authenticationToken, ChangeType changeType)
    {
        var user = new User { Id = "user", EmailAddress = "user@example.test", OrganizationIds = new HashSet<string> { "allowed" } };
        var oauth = new OAuthToken { Id = "oauth", UserId = user.Id, ClientId = "client", Resource = "https://localhost/api/v2", OrganizationIds = ["allowed"] };
        var access = new Token { Id = "access", Type = TokenType.Access, OrganizationId = "allowed" };
        var userAccess = new Token { Id = "user-access", Type = TokenType.Access, UserId = user.Id, OrganizationId = "allowed" };
        var session = new Token { Id = "session", Type = TokenType.Authentication, UserId = user.Id };
        ClaimsPrincipal[] identities = [new(user.ToIdentity(oauth)), new(access.ToIdentity()), new(user.ToIdentity(userAccess)), new(user.ToIdentity(session))];
        var registry = GetService<PushConnectionRegistry>();
        var sse = GetService<SseConnectionManager>();
        var webSockets = GetService<WebSocketConnectionManager>();
        var responses = identities.Select(_ => new FakeHttpResponse()).ToArray();
        var sockets = identities.Select(_ => new TestWebSocket()).ToArray();
        const string canary = "synthetic-new-credential-canary";

        try
        {
            for (int index = 0; index < identities.Length; index++)
            {
                Assert.True(PushPrincipal.TryCreate(identities[index], out var principal));
                Assert.Equal(index == 3, principal.CanReceiveTokenNotifications);
                sse.AddConnectionDeferred($"sse-{index}", responses[index], TestContext.Current.CancellationToken);
                webSockets.AddConnection($"websocket-{index}", sockets[index]);
                Assert.True(registry.TryRegister($"sse-{index}", principal.UserId, principal.TokenId, principal.OrganizationIds, principal.FollowMembershipAdditions, principal.CanReceiveTokenNotifications));
                Assert.True(registry.TryRegister($"websocket-{index}", principal.UserId, principal.TokenId, principal.OrganizationIds, principal.FollowMembershipAdditions, principal.CanReceiveTokenNotifications));
            }

            var broker = GetService<MessageBusBroker>();
            var logout = new EntityChanged { Type = nameof(Token), Id = "previous-session", ChangeType = ChangeType.Removed };
            logout.Data[ExtendedEntityChanged.KnownKeys.UserId] = user.Id;
            logout.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = true;
            await broker.OnEntityChangedAsync(logout, TestContext.Current.CancellationToken);
            Assert.NotNull(sse.GetConnectionById("sse-0"));
            Assert.NotNull(webSockets.GetConnectionById("websocket-0"));

            var message = new EntityChanged { Type = nameof(Token), Id = canary, ChangeType = changeType };
            message.Data[ExtendedEntityChanged.KnownKeys.IsAuthenticationToken] = authenticationToken;
            if (authenticationToken)
                message.Data[ExtendedEntityChanged.KnownKeys.UserId] = user.Id;
            else
                message.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = "allowed";

            await broker.OnEntityChangedAsync(message, TestContext.Current.CancellationToken);
            for (int index = 0; index < identities.Length; index++)
            {
                var connection = sse.GetConnectionById($"sse-{index}")!;
                connection.TryWriteKeepAlive();
                connection.Start();
                await sse.RemoveConnectionAsync($"sse-{index}");
            }

            Assert.Contains(canary, responses[3].WrittenData);
            Assert.Contains(canary, String.Join("", sockets[3].SentMessages));
            for (int index = 0; index < 3; index++)
            {
                Assert.DoesNotContain(canary, responses[index].WrittenData);
                Assert.DoesNotContain(canary, String.Join("", sockets[index].SentMessages));
            }
            Assert.Contains("sse-0", registry.GetUserConnections(user.Id));
        }
        finally
        {
            for (int index = 0; index < identities.Length; index++)
            {
                await sse.RemoveConnectionAsync($"sse-{index}");
                await webSockets.RemoveConnectionAsync($"websocket-{index}");
                registry.Unregister($"sse-{index}");
                registry.Unregister($"websocket-{index}");
                responses[index].Dispose();
                sockets[index].Dispose();
            }
        }
    }

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
