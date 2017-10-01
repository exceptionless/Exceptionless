using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Hubs {
    public sealed class MessageBusBroker {
        private static readonly string TokenTypeName = typeof(Token).Name;
        private static readonly string UserTypeName = typeof(User).Name;
        private readonly IConnectionManager _connectionManager;
        private readonly IConnectionMapping _connectionMapping;
        private readonly IMessageSubscriber _subscriber;
        private readonly ILogger _logger;

        public MessageBusBroker(IConnectionManager connectionManager, IConnectionMapping connectionMapping, IMessageSubscriber subscriber, ILogger<MessageBusBroker> logger) {
            _connectionManager = connectionManager;
            _connectionMapping = connectionMapping;
            _subscriber = subscriber;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken token) {
            _logger.LogDebug("Subscribing to message bus notifications");
            await _subscriber.SubscribeAsync<ExtendedEntityChanged>(OnEntityChangedAsync, token);
            await _subscriber.SubscribeAsync<PlanChanged>(OnPlanChangedAsync, token);
            await _subscriber.SubscribeAsync<PlanOverage>(OnPlanOverageAsync, token);
            await _subscriber.SubscribeAsync<UserMembershipChanged>(OnUserMembershipChangedAsync, token);
            await _subscriber.SubscribeAsync<ReleaseNotification>(OnReleaseNotificationAsync, token);
            await _subscriber.SubscribeAsync<SystemNotification>(OnSystemNotificationAsync, token);
            _logger.LogDebug("Subscribed to message bus notifications");
        }

        private async Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId)) {
                _logger.LogTrace("Ignoring User Membership Changed message: No organization id.");
                return;
            }

            // manage user organization group membership
            var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userMembershipChanged.UserId);
            _logger.LogTrace("Attempting to update user {user} active groups for {UserConnectionCount} connections", userMembershipChanged.UserId, userConnectionIds.Count);
            foreach (string connectionId in userConnectionIds) {
                if (userMembershipChanged.ChangeType == ChangeType.Added)
                    await _connectionMapping.GroupAddAsync(userMembershipChanged.OrganizationId, connectionId) ;
                else if (userMembershipChanged.ChangeType == ChangeType.Removed)
                    await _connectionMapping.GroupRemoveAsync(userMembershipChanged.OrganizationId, connectionId);
            }

            await GroupSendAsync(userMembershipChanged.OrganizationId, userMembershipChanged);
        }

        private async Task OnEntityChangedAsync(ExtendedEntityChanged entityChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (entityChanged == null)
                return;

            if (UserTypeName == entityChanged.Type) {
                // It's pointless to send a user added message to the new user.
                if (entityChanged.ChangeType == ChangeType.Added) {
                    _logger.LogTrace("Ignoring {UserTypeName} message for added user: {user}.", UserTypeName, entityChanged.Id);
                    return;
                }

                var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(entityChanged.Id);
                _logger.LogTrace("Sending {UserTypeName} message to user: {user} (to {UserConnectionCount} connections)", UserTypeName, entityChanged.Id, userConnectionIds.Count);
                foreach (string connectionId in userConnectionIds)
                    await Context.Connection.TypedSendAsync(connectionId, entityChanged);

                return;
            }

            // Only allow specific token messages to be sent down to the client.
            if (TokenTypeName == entityChanged.Type) {
                string userId = entityChanged.Data.GetValueOrDefault<string>("UserId");
                if (userId != null) {
                    var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userId);
                    _logger.LogTrace("Sending {TokenTypeName} message for added user: {user} (to {UserConnectionCount} connections)", TokenTypeName, userId, userConnectionIds.Count);
                    foreach (string connectionId in userConnectionIds)
                        await Context.Connection.TypedSendAsync(connectionId, entityChanged);

                    return;
                }

                if (entityChanged.Data.GetValueOrDefault<bool>("IsAuthenticationToken")) {
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

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planOverage != null) {
                _logger.LogTrace("Sending plan overage message to organization: {organization}", planOverage.OrganizationId);
                return GroupSendAsync(planOverage.OrganizationId, planOverage);
            }

            return Task.CompletedTask;
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planChanged != null) {
                _logger.LogTrace("Sending plan changed message to organization: {organization}", planChanged.OrganizationId);
                return GroupSendAsync(planChanged.OrganizationId, planChanged);
            }

            return Task.CompletedTask;
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            _logger.LogTrace("Sending release notification message: {Message}", notification.Message);
            return Context.Connection.TypedBroadcastAsync(notification);
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            _logger.LogTrace("Sending system notification message: {Message}", notification.Message);
            return Context.Connection.TypedBroadcastAsync(notification);
        }

        private async Task GroupSendAsync(string group, object value) {
            var connectionIds = await _connectionMapping.GetGroupConnectionsAsync(group);
            if (connectionIds.Count == 0) {
                _logger.LogTrace("Ignoring group message to {Group}: No Connections", group);
                return;
            }

            await Context.Connection.TypedSendAsync(connectionIds.ToList(), value);
        }

        private IPersistentConnectionContext Context => _connectionManager.GetConnectionContext<MessageBusConnection>();
    }

    public static class MessageBrokerExtensions {
        public static Task TypedSendAsync(this IConnection connection, string connectionId, object value) {
            return connection.Send(connectionId, new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        public static Task TypedSendAsync(this IConnection connection, IList<string> connectionIds, object value) {
            return connection.Send(connectionIds, new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        public static Task TypedBroadcastAsync(this IConnection connection, object value) {
            return connection.Broadcast(new TypedMessage { Type = GetMessageType(value), Message = value });
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