using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;

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

        public void Start() {
            _subscriber.Subscribe<ExtendedEntityChanged>(OnEntityChangedAsync);
            _subscriber.Subscribe<PlanChanged>(OnPlanChangedAsync);
            _subscriber.Subscribe<PlanOverage>(OnPlanOverageAsync);
            _subscriber.Subscribe<UserMembershipChanged>(OnUserMembershipChangedAsync);
            _subscriber.Subscribe<ReleaseNotification>(OnReleaseNotificationAsync);
            _subscriber.Subscribe<SystemNotification>(OnSystemNotificationAsync);
        }

        private async Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId)) {
                _logger.Trace(() => $"Ignoring User Membership Changed message: No organization id.");
                return;
            }

            // manage user organization group membership
            var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userMembershipChanged.UserId);
            _logger.Trace(() => $"Attempting to update user {userMembershipChanged.UserId} active groups for {userConnectionIds.Count} connections");
            foreach (var connectionId in userConnectionIds) {
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
                    _logger.Trace(() => $"Ignoring {UserTypeName} message for added user: {entityChanged.Id}.");
                    return;
                }

                var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(entityChanged.Id);
                _logger.Trace(() => $"Sending {UserTypeName} message to user: {entityChanged.Id} (to {userConnectionIds.Count} connections)");
                foreach (var connectionId in userConnectionIds)
                    await Context.Connection.TypedSendAsync(connectionId, entityChanged);

                return;
            }

            // Only allow specific token messages to be sent down to the client.
            if (TokenTypeName == entityChanged.Type) {
                string userId = entityChanged.Data.GetValueOrDefault<string>("UserId");
                if (userId != null) {
                    var userConnectionIds = await _connectionMapping.GetUserIdConnectionsAsync(userId);
                    _logger.Trace(() => $"Sending {TokenTypeName} message for added user: {userId} (to {userConnectionIds.Count} connections)");
                    foreach (var connectionId in userConnectionIds)
                        await Context.Connection.TypedSendAsync(connectionId, entityChanged);

                    return;
                }

                if (entityChanged.Data.GetValueOrDefault<bool>("IsAuthenticationToken")) {
                    _logger.Trace(() => $"Ignoring {TokenTypeName} Authentication Token message: {entityChanged.Id}.");
                    return;
                }

                entityChanged.Data.Clear();
            }

            if (!String.IsNullOrEmpty(entityChanged.OrganizationId)) {
                _logger.Trace(() => $"Sending {entityChanged.Type} message to organization: {entityChanged.OrganizationId})");
                await GroupSendAsync(entityChanged.OrganizationId, entityChanged);
            }
        }

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planOverage != null) {
                _logger.Trace(() => $"Sending plan overage message to organization: {planOverage.OrganizationId})");
                return GroupSendAsync(planOverage.OrganizationId, planOverage);
            }

            return Task.CompletedTask;
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planChanged != null) {
                _logger.Trace(() => $"Sending plan changed message to organization: {planChanged.OrganizationId})");
                return GroupSendAsync(planChanged.OrganizationId, planChanged);
            }

            return Task.CompletedTask;
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            _logger.Trace(() => $"Sending release notification message: {notification.Message})");
            return Context.Connection.TypedBroadcastAsync(notification);
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            _logger.Trace(() => $"Sending system notification message: {notification.Message})");
            return Context.Connection.TypedBroadcastAsync(notification);
        }

        private async Task GroupSendAsync(string group, object value) {
            var connectionIds = await _connectionMapping.GetGroupConnectionsAsync(group);
            if (connectionIds.Count == 0) {
                _logger.Trace(() => $"Ignoring group message to {group}: No Connections");
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