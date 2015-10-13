using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Foundatio.Messaging;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;

namespace Exceptionless.Api.Hubs {
    public class MessageBusBroker {
        private readonly IConnectionManager _connectionManager;
        private readonly ConnectionMapping _userIdConnections;

        public MessageBusBroker(IConnectionManager connectionManager, ConnectionMapping userIdConnections, IMessageSubscriber subscriber) {
            _connectionManager = connectionManager;
            _userIdConnections = userIdConnections;

            subscriber.Subscribe<EntityChanged>(OnEntityChangedAsync);
            subscriber.Subscribe<PlanChanged>(OnPlanChangedAsync);
            subscriber.Subscribe<PlanOverage>(OnPlanOverageAsync);
            subscriber.Subscribe<UserMembershipChanged>(OnUserMembershipChangedAsync);
            subscriber.Subscribe<ReleaseNotification>(OnReleaseNotificationAsync);
            subscriber.Subscribe<SystemNotification>(OnSystemNotificationAsync);
        }

        private async Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId))
                return;

            // manage user organization group membership
            foreach (var connectionId in _userIdConnections.GetConnections(userMembershipChanged.UserId)) {
                if (userMembershipChanged.ChangeType == ChangeType.Added)
                    await Context.Groups.Add(connectionId, userMembershipChanged.OrganizationId);
                else if (userMembershipChanged.ChangeType == ChangeType.Removed)
                    await Context.Groups.Remove(connectionId, userMembershipChanged.OrganizationId);
            }

            await Context.Groups.TypedSend(userMembershipChanged.OrganizationId, userMembershipChanged);
        }

        private async Task OnEntityChangedAsync(EntityChanged entityChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (entityChanged == null)
                return;

            if (entityChanged.Type == typeof(User).Name) {
                foreach (var connectionId in _userIdConnections.GetConnections(entityChanged.Id))
                    await Context.Connection.TypedSend(connectionId, entityChanged);

                return;
            }

            if (!String.IsNullOrEmpty(entityChanged.OrganizationId))
                await Context.Groups.TypedSend(entityChanged.OrganizationId, entityChanged);
        }

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planOverage != null)
                return Context.Groups.TypedSend(planOverage.OrganizationId, planOverage);

            return TaskHelper.Completed();
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planChanged != null)
                return Context.Groups.TypedSend(planChanged.OrganizationId, planChanged);

            return TaskHelper.Completed();
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            return Context.Connection.TypedBroadcast(notification);
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            return Context.Connection.TypedBroadcast(notification);
        }

        private IPersistentConnectionContext _context;
        private IPersistentConnectionContext Context => _context ?? (_context = _connectionManager.GetConnectionContext<MessageBusConnection>());
    }

    public static class MessageBrokerExtensions {
        public static Task TypedSend(this IConnection connection, string connectionId, object value) {
            return connection.Send(connectionId, new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        public static Task TypedBroadcast(this IConnection connection, object value) {
            return connection.Broadcast(new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        public static Task TypedSend(this IConnectionGroupManager group, string name, object value) {
            return group.Send(name, new TypedMessage { Type = GetMessageType(value), Message = value });
        }

        private static string GetMessageType(object value) {
            if (value.GetType() == typeof(EntityChanged))
                return String.Concat(((EntityChanged)value).Type, "Changed");

            return value.GetType().Name;
        }
    }

    public class TypedMessage {
        public string Type { get; set; }
        public object Message { get; set; }
    }
}