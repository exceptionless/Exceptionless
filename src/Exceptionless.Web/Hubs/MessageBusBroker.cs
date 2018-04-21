using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Hubs {
    public sealed class MessageBusBroker : IStartupAction {
        private static readonly string TokenTypeName = typeof(Token).Name;
        private static readonly string UserTypeName = typeof(User).Name;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IConnectionMapping _connectionMapping;
        private readonly IMessageSubscriber _subscriber;
        private readonly ILogger _logger;

        public MessageBusBroker(WebSocketConnectionManager connectionManager, IConnectionMapping connectionMapping, IMessageSubscriber subscriber, ILogger<MessageBusBroker> logger) {
            _connectionManager = connectionManager;
            _connectionMapping = connectionMapping;
            _subscriber = subscriber;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken shutdownToken = default) {
            if (!Settings.Current.EnableWebSockets)
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

        private async Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId)) {
                _logger.LogTrace("Ignoring User Membership Changed message: No organization id.");
                return;
            }

            // manage user organization group membership
            var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userMembershipChanged.UserId);
            _logger.LogTrace("Attempting to update user {User} active groups for {UserConnectionCount} connections", userMembershipChanged.UserId, userConnectionIds.Count);
            foreach (string connectionId in userConnectionIds) {
                if (userMembershipChanged.ChangeType == ChangeType.Added)
                    await _connectionMapping.GroupAddAsync(userMembershipChanged.OrganizationId, connectionId) ;
                else if (userMembershipChanged.ChangeType == ChangeType.Removed)
                    await _connectionMapping.GroupRemoveAsync(userMembershipChanged.OrganizationId, connectionId);
            }

            await GroupSendAsync(userMembershipChanged.OrganizationId, userMembershipChanged);
        }

        private async Task OnEntityChangedAsync(EntityChanged ec, CancellationToken cancellationToken = default) {
            if (ec == null)
                return;

            var entityChanged = ExtendedEntityChanged.Create(ec);
            if (UserTypeName == entityChanged.Type) {
                // It's pointless to send a user added message to the new user.
                if (entityChanged.ChangeType == ChangeType.Added) {
                    _logger.LogTrace("Ignoring {UserTypeName} message for added user: {user}.", UserTypeName, entityChanged.Id);
                    return;
                }

                var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(entityChanged.Id);
                _logger.LogTrace("Sending {UserTypeName} message to user: {user} (to {UserConnectionCount} connections)", UserTypeName, entityChanged.Id, userConnectionIds.Count);
                foreach (string connectionId in userConnectionIds)
                    await TypedSendAsync(connectionId, entityChanged);

                return;
            }

            // Only allow specific token messages to be sent down to the client.
            if (TokenTypeName == entityChanged.Type) {
                string userId = entityChanged.Data.GetValueOrDefault<string>(ExtendedEntityChanged.KnownKeys.UserId);
                if (userId != null) {
                    var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userId);
                    _logger.LogTrace("Sending {TokenTypeName} message for added user: {user} (to {UserConnectionCount} connections)", TokenTypeName, userId, userConnectionIds.Count);
                    foreach (string connectionId in userConnectionIds)
                        await TypedSendAsync(connectionId, entityChanged);

                    return;
                }

                if (entityChanged.Data.GetValueOrDefault<bool>(ExtendedEntityChanged.KnownKeys.IsAuthenticationToken)) {
                    _logger.LogTrace("Ignoring {TokenTypeName} Authentication Token message: {user}.", TokenTypeName, entityChanged.Id);
                    return;
                }

                entityChanged.Data.Clear();
            }

            if (!String.IsNullOrEmpty(entityChanged.OrganizationId)) {
                _logger.LogTrace("Sending {MessageType} message to organization: {organization}", entityChanged.Type, entityChanged.OrganizationId);
                await GroupSendAsync(entityChanged.OrganizationId, entityChanged);
            }
        }

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default) {
            if (planOverage != null) {
                _logger.LogTrace("Sending plan overage message to organization: {organization}", planOverage.OrganizationId);
                return GroupSendAsync(planOverage.OrganizationId, planOverage);
            }

            return Task.CompletedTask;
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default) {
            if (planChanged != null) {
                _logger.LogTrace("Sending plan changed message to organization: {organization}", planChanged.OrganizationId);
                return GroupSendAsync(planChanged.OrganizationId, planChanged);
            }

            return Task.CompletedTask;
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default) {
            _logger.LogTrace("Sending release notification message: {Message}", notification.Message);
            return TypedBroadcastAsync(notification);
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default) {
            _logger.LogTrace("Sending system notification message: {Message}", notification.Message);
            return TypedBroadcastAsync(notification);
        }

        private async Task GroupSendAsync(string group, object value) {
            var connectionIds = await _connectionMapping.GetGroupConnectionsAsync(group);
            if (connectionIds.Count == 0) {
                _logger.LogTrace("Ignoring group message to {Group}: No Connections", group);
                return;
            }

            await TypedSendAsync(connectionIds.ToList(), value);
        }

        public Task TypedSendAsync(string connectionId, object value) {
            return _connectionManager.SendMessageAsync(connectionId, new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        public Task TypedSendAsync(IList<string> connectionIds, object value) {
            return _connectionManager.SendMessageAsync(connectionIds, new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        public Task TypedBroadcastAsync(object value) {
            return _connectionManager.SendMessageToAllAsync(new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        private static string GetMessageType(object value) {
            if (value is EntityChanged)
                return String.Concat(((EntityChanged)value).Type, "Changed");

            return value.GetType().Name;
        }
    }

    public class TypedMessage {
        public string Type { get; set; }
        public object Message { get; set; }
    }
}