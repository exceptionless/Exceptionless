#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Exceptionless.Api.Hubs {
    [HubName("messages")]
    public class MessageBusHub : Hub {
        private readonly ConnectionMapping _userIdConnections = new ConnectionMapping();

        public MessageBusHub(IMessageSubscriber subscriber) {
            subscriber.Subscribe<EntityChanged>(OnEntityChanged);
            subscriber.Subscribe<EventOccurrence>(OnEventOccurrence);
            subscriber.Subscribe<PlanChanged>(OnPlanChanged);
            subscriber.Subscribe<PlanOverage>(OnPlanOverage);
            subscriber.Subscribe<StackUpdated>(OnStackUpdated);
            subscriber.Subscribe<UserMembershipChanged>(OnUserMembershipChanged);
        }

        private void OnUserMembershipChanged(UserMembershipChanged userMembershipChanged) {
            if (String.IsNullOrEmpty(userMembershipChanged.OrganizationId))
                return;

            // manage user organization group membership
            var connectionId = _userIdConnections.GetConnections(userMembershipChanged.UserId).FirstOrDefault();
            if (connectionId != null && userMembershipChanged.ChangeType == ChangeType.Added)
                Groups.Add(connectionId, userMembershipChanged.OrganizationId);
            else if (connectionId != null && userMembershipChanged.ChangeType == ChangeType.Removed)
                Groups.Remove(connectionId, userMembershipChanged.OrganizationId);

            Clients.Group(userMembershipChanged.OrganizationId).userMembershipChanged(userMembershipChanged);
        }

        private void OnEntityChanged(EntityChanged entityChanged) {
            if (entityChanged.Type == typeof(User).Name && Clients.User(entityChanged.Id) != null)
                Clients.User(entityChanged.Id).entityChanged(entityChanged);

            if (String.IsNullOrEmpty(entityChanged.OrganizationId))
                return;

            Clients.Group(entityChanged.OrganizationId).entityChanged(entityChanged);
        }

        private void OnEventOccurrence(EventOccurrence eventOccurrence) {
            Clients.Group(eventOccurrence.OrganizationId).eventOccurrence(eventOccurrence);
        }

        private void OnStackUpdated(StackUpdated stackUpdated) {
            Clients.Group(stackUpdated.OrganizationId).stackUpdated(stackUpdated);
        }

        private void OnPlanOverage(PlanOverage planOverage) {
            Clients.Group(planOverage.OrganizationId).planOverage(planOverage);
        }

        private void OnPlanChanged(PlanChanged planChanged) {
            Clients.Group(planChanged.OrganizationId).planChanged(planChanged);
        }

        public override Task OnConnected() {
            foreach (string organizationId in Context.User.GetOrganizationIds())
                Groups.Add(Context.ConnectionId, organizationId);

            _userIdConnections.Add(Context.User.GetUserId(), Context.ConnectionId);

            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled) {
            _userIdConnections.Remove(Context.User.GetUserId(), Context.ConnectionId);

            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected() {
            if (!_userIdConnections.GetConnections(Context.User.GetUserId()).Contains(Context.ConnectionId))
                _userIdConnections.Add(Context.User.GetUserId(), Context.ConnectionId);

            return base.OnReconnected();
        }
    }
}