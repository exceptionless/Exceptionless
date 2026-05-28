using Exceptionless.Core;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Foundatio.Utility;

namespace Exceptionless.Web.Hubs;

public sealed class MessageBusBroker : IStartupAction
{
    private static readonly string TokenTypeName = nameof(Token);
    private static readonly string UserTypeName = nameof(User);
    private readonly SseConnectionManager _sseConnectionManager;
    private readonly WebSocketConnectionManager _webSocketConnectionManager;
    private readonly IConnectionMapping _connectionMapping;
    private readonly IMessageSubscriber _subscriber;
    private readonly AppOptions _options;
    private readonly ILogger _logger;

    public MessageBusBroker(SseConnectionManager sseConnectionManager, WebSocketConnectionManager webSocketConnectionManager, IConnectionMapping connectionMapping, IMessageSubscriber subscriber, AppOptions options, ILogger<MessageBusBroker> logger)
    {
        _sseConnectionManager = sseConnectionManager;
        _webSocketConnectionManager = webSocketConnectionManager;
        _connectionMapping = connectionMapping;
        _subscriber = subscriber;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken shutdownToken = default)
    {
        if (!_options.EnablePush)
            return;

        _logger.LogDebug("Subscribing to message bus notifications");
        await Task.WhenAll(
            _subscriber.SubscribeAsync<EntityChanged>(OnEntityChangedAsync, shutdownToken),
            _subscriber.SubscribeAsync<PlanChanged>(OnPlanChangedAsync, shutdownToken),
            _subscriber.SubscribeAsync<PlanOverage>(OnPlanOverageAsync, shutdownToken),
            _subscriber.SubscribeAsync<UserMembershipChanged>(OnUserMembershipChangedAsync, shutdownToken),
            _subscriber.SubscribeAsync<ReleaseNotification>(OnReleaseNotificationAsync, shutdownToken),
            _subscriber.SubscribeAsync<SystemNotification>(OnSystemNotificationAsync, shutdownToken)
        );
        _logger.LogDebug("Subscribed to message bus notifications");
    }

    private async Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId))
        {
            _logger.LogTrace("Ignoring User Membership Changed message: No organization id");
            return;
        }

        // manage user organization group membership
        var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userMembershipChanged.UserId);
        _logger.LogTrace("Attempting to update user {User} active groups for {UserConnectionCount} connections", userMembershipChanged.UserId, userConnectionIds.Count);
        foreach (string connectionId in userConnectionIds)
        {
            if (userMembershipChanged.ChangeType is ChangeType.Added)
                await _connectionMapping.GroupAddAsync(userMembershipChanged.OrganizationId, connectionId);
            else if (userMembershipChanged.ChangeType is ChangeType.Removed)
                await _connectionMapping.GroupRemoveAsync(userMembershipChanged.OrganizationId, connectionId);
        }

        await GroupSendAsync(userMembershipChanged.OrganizationId, userMembershipChanged);
    }

    internal async Task OnEntityChangedAsync(EntityChanged ec, CancellationToken cancellationToken = default)
    {
        if (ec is null)
            return;

        var entityChanged = ExtendedEntityChanged.Create(ec);
        if (String.Equals(UserTypeName, entityChanged.Type, StringComparison.Ordinal))
        {
            // It's pointless to send a user added message to the new user.
            if (entityChanged.ChangeType is ChangeType.Added)
            {
                _logger.LogTrace("Ignoring {UserTypeName} message for added user: {UserId}", UserTypeName, entityChanged.Id);
                return;
            }

            if (entityChanged.Id is null)
            {
                _logger.LogTrace("Ignoring {UserTypeName} message: No user id", UserTypeName);
                return;
            }

            var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(entityChanged.Id);
            _logger.LogTrace("Sending {UserTypeName} message to user: {UserId} (to {UserConnectionCount} connections)", UserTypeName, entityChanged.Id, userConnectionIds.Count);
            foreach (string connectionId in userConnectionIds)
                TypedSend(connectionId, entityChanged);

            return;
        }

        // Only allow specific token messages to be sent down to the client.
        if (String.Equals(TokenTypeName, entityChanged.Type, StringComparison.Ordinal))
        {
            string? userId = entityChanged.Data.GetValueOrDefault<string>(ExtendedEntityChanged.KnownKeys.UserId);
            bool isAuthToken = entityChanged.Data.GetValueOrDefault<bool>(ExtendedEntityChanged.KnownKeys.IsAuthenticationToken);

            if (userId is not null)
            {
                var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userId);

                // Auth token removed = logout. Close connections immediately without sending;
                // there is no point delivering a message to a connection we are about to tear down.
                if (isAuthToken && entityChanged.ChangeType is ChangeType.Removed)
                {
                    _logger.LogTrace("Auth token removed for user {UserId}; closing {ConnectionCount} push connection(s)", userId, userConnectionIds.Count);
                    string? organizationId = entityChanged.OrganizationId;
                    foreach (string connectionId in userConnectionIds)
                    {
                        if (organizationId is { Length: > 0 })
                            await _connectionMapping.GroupRemoveAsync(organizationId, connectionId);

                        await _connectionMapping.UserIdRemoveAsync(userId, connectionId);
                        await _sseConnectionManager.RemoveConnectionAsync(connectionId);
                        await _webSocketConnectionManager.RemoveConnectionAsync(connectionId);
                    }

                    return;
                }

                _logger.LogTrace("Sending {TokenTypeName} message for user: {UserId} (to {UserConnectionCount} connections)", TokenTypeName, userId, userConnectionIds.Count);
                foreach (string connectionId in userConnectionIds)
                    TypedSend(connectionId, entityChanged);

                return;
            }

            if (isAuthToken)
            {
                _logger.LogTrace("Ignoring {TokenTypeName} Authentication Token message: {TokenId}", TokenTypeName, entityChanged.Id);
                return;
            }

            entityChanged.Data.Clear();
        }

        if (!String.IsNullOrEmpty(entityChanged.OrganizationId))
        {
            _logger.LogTrace("Sending {MessageType} message to organization: {Organization}", entityChanged.Type, entityChanged.OrganizationId);
            await GroupSendAsync(entityChanged.OrganizationId, entityChanged);
        }
    }

    private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default)
    {
        if (planOverage is not null)
        {
            _logger.LogTrace("Sending plan overage message to organization: {Organization}", planOverage.OrganizationId);
            return GroupSendAsync(planOverage.OrganizationId, planOverage);
        }

        return Task.CompletedTask;
    }

    private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default)
    {
        if (planChanged is not null)
        {
            _logger.LogTrace("Sending plan changed message to organization: {Organization}", planChanged.OrganizationId);
            return GroupSendAsync(planChanged.OrganizationId, planChanged);
        }

        return Task.CompletedTask;
    }

    private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Sending release notification message: {Message}", notification.Message);
        TypedBroadcast(notification);
        return Task.CompletedTask;
    }

    private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Sending system notification message: {Message}", notification.Message);
        TypedBroadcast(notification);
        return Task.CompletedTask;
    }

    private async Task GroupSendAsync(string group, object value)
    {
        var connectionIds = await _connectionMapping.GetGroupConnectionsAsync(group);
        if (connectionIds.Count is 0)
        {
            _logger.LogTrace("Ignoring group message to {Group}: No Connections", group);
            return;
        }

        TypedSend(connectionIds, value);
    }

    public void TypedSend(string connectionId, object value)
    {
        var message = new TypedMessage { Type = GetMessageType(value), Message = value };
        bool canDrop = CanDrop(value);
        _sseConnectionManager.SendMessage(connectionId, message, canDrop);
        _webSocketConnectionManager.SendMessage(connectionId, message);
    }

    public void TypedSend(IEnumerable<string> connectionIds, object value)
    {
        var message = new TypedMessage { Type = GetMessageType(value), Message = value };
        bool canDrop = CanDrop(value);
        _sseConnectionManager.SendMessage(connectionIds, message, canDrop);
        _webSocketConnectionManager.SendMessage(connectionIds, message);
    }

    public void TypedBroadcast(object value)
    {
        var message = new TypedMessage { Type = GetMessageType(value), Message = value };
        bool canDrop = CanDrop(value);
        _sseConnectionManager.SendMessageToAll(message, canDrop);
        _webSocketConnectionManager.SendMessageToAll(message);
    }

    private static string GetMessageType(object value)
    {
        if (value is EntityChanged)
            return String.Concat(((EntityChanged)value).Type, "Changed");

        return value.GetType().Name;
    }

    private static bool CanDrop(object value)
    {
        return value is not (PlanOverage or ReleaseNotification or SystemNotification);
    }
}

public record TypedMessage
{
    public required string Type { get; set; }
    public required object Message { get; set; }
}
