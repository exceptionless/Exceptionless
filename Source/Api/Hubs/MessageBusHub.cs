using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
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
            subscriber.Subscribe<EntityChanged>(OnEntityChangedAsync);
            subscriber.Subscribe<PlanChanged>(OnPlanChangedAsync);
            subscriber.Subscribe<PlanOverage>(OnPlanOverageAsync);
            subscriber.Subscribe<UserMembershipChanged>(OnUserMembershipChangedAsync);
            subscriber.Subscribe<ReleaseNotification>(OnReleaseNotificationAsync);
            subscriber.Subscribe<SystemNotification>(OnSystemNotificationAsync);
        }

        private Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken) {
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId))
                return TaskHelper.Completed();

            // manage user organization group membership
            foreach (var connectionId in _userIdConnections.GetConnections(userMembershipChanged.UserId)) {
                if (userMembershipChanged.ChangeType == ChangeType.Added)
                    Groups.Add(connectionId, userMembershipChanged.OrganizationId);
                else if (userMembershipChanged.ChangeType == ChangeType.Removed)
                    Groups.Remove(connectionId, userMembershipChanged.OrganizationId);
            }

            try {
                Clients.Group(userMembershipChanged.OrganizationId).userMembershipChanged(userMembershipChanged);
            } catch (NullReferenceException) { } // TODO: Remove this when SignalR bug is fixed.

            return TaskHelper.Completed();
        }

        private Task OnEntityChangedAsync(EntityChanged entityChanged, CancellationToken cancellationToken) {
            if (entityChanged == null)
                return TaskHelper.Completed();

            if (entityChanged.Type == typeof(User).Name && Clients.User(entityChanged.Id) != null) {
                try {
                    Clients.User(entityChanged.Id).entityChanged(entityChanged);
                } catch (NullReferenceException) { } // TODO: Remove this when SignalR bug is fixed.
                return TaskHelper.Completed();
            }

            if (String.IsNullOrEmpty(entityChanged.OrganizationId))
                return TaskHelper.Completed();

            try {
                Clients.Group(entityChanged.OrganizationId).entityChanged(entityChanged);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.

            return TaskHelper.Completed();
        }

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken) {
            if (planOverage == null)
                return TaskHelper.Completed();

            try {
                Clients.Group(planOverage.OrganizationId).planOverage(planOverage);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.

            return TaskHelper.Completed();
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken) {
            if (planChanged == null)
                return TaskHelper.Completed();
            
            try {
                Clients.Group(planChanged.OrganizationId).planChanged(planChanged);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.

            return TaskHelper.Completed();
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken) {
            try {
                Clients.All.releaseNotification(notification);
            } catch (NullReferenceException) {} // TODO: Remove this when SignalR bug is fixed.

            return TaskHelper.Completed();
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken) {
            try {
                Clients.All.systemNotification(notification);
            } catch (NullReferenceException) { } // TODO: Remove this when SignalR bug is fixed.

            return TaskHelper.Completed();
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