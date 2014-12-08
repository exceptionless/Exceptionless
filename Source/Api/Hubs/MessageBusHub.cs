#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Exceptionless.Api.Hubs {
    [HubName("messages")]
    public class MessageBusHub : Hub {
        public MessageBusHub(IMessageSubscriber subscriber) {
            subscriber.Subscribe<EntityChanged>(OnEntityChanged);
            subscriber.Subscribe<EventOccurrence>(OnEventOccurrence);
            subscriber.Subscribe<PlanChanged>(OnPlanChanged);
            subscriber.Subscribe<PlanOverage>(OnPlanOverage);
            subscriber.Subscribe<StackUpdated>(OnStackUpdated);
        }

        private void OnEntityChanged(EntityChanged entityChanged) {
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

            return base.OnConnected();
        }
    }
}