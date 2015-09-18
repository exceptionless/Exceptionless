using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Foundatio.Messaging;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Exceptionless.Api.Hubs {
    public interface IMessageBusHubClientMethods {
        void entityChanged(EntityChanged entityChanged);
        void planChanged(PlanChanged planChanged);
        void planOverage(PlanOverage planOverage);
        void userMembershipChanged(UserMembershipChanged userMembershipChanged);
        void releaseNotification(ReleaseNotification notification);
        void systemNotification(SystemNotification notification);
    }

    [HubName("messages")]
    public class MessageBusHub : Hub<IMessageBusHubClientMethods> {
        private readonly ConnectionMapping _userIdConnections = new ConnectionMapping();

        public MessageBusHub(IMessageSubscriber subscriber) {
            subscriber.Subscribe<EntityChanged>(OnEntityChanged);
            subscriber.Subscribe<PlanChanged>(OnPlanChanged);
            subscriber.Subscribe<PlanOverage>(OnPlanOverage);
            subscriber.Subscribe<UserMembershipChanged>(OnUserMembershipChanged);
            subscriber.Subscribe<ReleaseNotification>(OnReleaseNotification);
            subscriber.Subscribe<SystemNotification>(OnSystemNotification);
        }

        private void OnUserMembershipChanged(UserMembershipChanged userMembershipChanged) {
            if (userMembershipChanged == null)
                return;

            if (String.IsNullOrEmpty(userMembershipChanged.OrganizationId))
                return;

            // manage user organization group membership
            foreach (var connectionId in _userIdConnections.GetConnections(userMembershipChanged.UserId)) {
                if (userMembershipChanged.ChangeType == ChangeType.Added)
                    await Groups.Add(connectionId, userMembershipChanged.OrganizationId).AnyContext();
                else if (userMembershipChanged.ChangeType == ChangeType.Removed)
                    await Groups.Remove(connectionId, userMembershipChanged.OrganizationId).AnyContext();
            }

            try
            {
                Clients.Group(userMembershipChanged.OrganizationId).userMembershipChanged(userMembershipChanged);
            } catch (NullReferenceException) { } // TODO: Remove this when SignalR bug is fixed.
        }

        private void OnEntityChanged(EntityChanged entityChanged) {
            if (entityChanged == null)
                return;

            if (entityChanged.Type == typeof(User).Name && Clients.User(entityChanged.Id) != null) {
                try {
                    Clients.User(entityChanged.Id).entityChanged(entityChanged);
                } catch (NullReferenceException) { } // TODO: Remove this when SignalR bug is fixed.
                return;
            }

            if (String.IsNullOrEmpty(entityChanged.OrganizationId))
                return;

            try {
                Clients.Group(entityChanged.OrganizationId).entityChanged(entityChanged);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.
        }

        private void OnPlanOverage(PlanOverage planOverage) {
            if (planOverage == null)
                return;

            try {
                Clients.Group(planOverage.OrganizationId).planOverage(planOverage);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.
        }

        private void OnPlanChanged(PlanChanged planChanged) {
            if (planChanged == null)
                return;
            
            try {
                Clients.Group(planChanged.OrganizationId).planChanged(planChanged);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.
        }

        private void OnReleaseNotification(ReleaseNotification notification) {
            try {
                Clients.All.releaseNotification(notification);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.
        }

        private void OnSystemNotification(SystemNotification notification) {
            try {
                Clients.All.systemNotification(notification);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.
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
            foreach (string organizationId in Context.User.GetOrganizationIds())
                if (organizationId != null)
                    Groups.Add(Context.ConnectionId, organizationId);

            if (!_userIdConnections.GetConnections(Context.User.GetUserId()).Contains(Context.ConnectionId))
                _userIdConnections.Add(Context.User.GetUserId(), Context.ConnectionId);

            return base.OnReconnected();
        }
    }
}