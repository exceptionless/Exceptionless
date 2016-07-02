﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;

namespace Exceptionless.Api.Hubs {
    public sealed class MessageBusBroker {
        private readonly IConnectionManager _connectionManager;
        private readonly IConnectionMapping _connectionMapping;
        private readonly IMessageSubscriber _subscriber;

        public MessageBusBroker(IConnectionManager connectionManager, IConnectionMapping connectionMapping, IMessageSubscriber subscriber) {
            _connectionManager = connectionManager;
            _connectionMapping = connectionMapping;
            _subscriber = subscriber;
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
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId))
                return;

            // manage user organization group membership
            foreach (var connectionId in await _connectionMapping.GetConnectionsAsync(userMembershipChanged.UserId)) {
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

            if (entityChanged.Type == typeof(User).Name) {
                foreach (var connectionId in await _connectionMapping.GetConnectionsAsync(entityChanged.Id))
                    await GroupSendAsync(connectionId, entityChanged);

                return;
            }

            if (!String.IsNullOrEmpty(entityChanged.OrganizationId))
                await GroupSendAsync(entityChanged.OrganizationId, entityChanged);
        }

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planOverage != null)
                return GroupSendAsync(planOverage.OrganizationId, planOverage);

            return Task.CompletedTask;
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planChanged != null)
                return GroupSendAsync(planChanged.OrganizationId, planChanged);

            return Task.CompletedTask;
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            return Context.Connection.TypedBroadcastAsync(notification);
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            return Context.Connection.TypedBroadcastAsync(notification);
        }

        private async Task GroupSendAsync(string group, object value) {
            var connectionIds = await _connectionMapping.GetGroupConnectionsAsync(group).AnyContext();
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
