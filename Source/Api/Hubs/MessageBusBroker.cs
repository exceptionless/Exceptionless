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

        private Task OnUserMembershipChangedAsync(UserMembershipChanged userMembershipChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (String.IsNullOrEmpty(userMembershipChanged?.OrganizationId))
                return TaskHelper.Completed();

            // manage user organization group membership
            foreach (var connectionId in _userIdConnections.GetConnections(userMembershipChanged.UserId)) {
                if (userMembershipChanged.ChangeType == ChangeType.Added)
                    HubContext.Groups.Add(connectionId, userMembershipChanged.OrganizationId);
                else if (userMembershipChanged.ChangeType == ChangeType.Removed)
                    HubContext.Groups.Remove(connectionId, userMembershipChanged.OrganizationId);
            }
            
            HubContext.Clients.Group(userMembershipChanged.OrganizationId).userMembershipChanged(userMembershipChanged);

            return TaskHelper.Completed();
        }

        private Task OnEntityChangedAsync(EntityChanged entityChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (entityChanged == null)
                return TaskHelper.Completed();

            if (entityChanged.Type == typeof(User).Name && HubContext.Clients.User(entityChanged.Id) != null) {
                HubContext.Clients.User(entityChanged.Id).entityChanged(entityChanged);
                return TaskHelper.Completed();
            }

            if (!String.IsNullOrEmpty(entityChanged.OrganizationId))
                HubContext.Clients.Group(entityChanged.OrganizationId).entityChanged(entityChanged);

            return TaskHelper.Completed();
        }

        private Task OnPlanOverageAsync(PlanOverage planOverage, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planOverage != null)
                HubContext.Clients.Group(planOverage.OrganizationId).planOverage(planOverage);

            return TaskHelper.Completed();
        }

        private Task OnPlanChangedAsync(PlanChanged planChanged, CancellationToken cancellationToken = default(CancellationToken)) {
            if (planChanged != null)
                HubContext.Clients.Group(planChanged.OrganizationId).planChanged(planChanged);

            return TaskHelper.Completed();
        }

        private Task OnReleaseNotificationAsync(ReleaseNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            HubContext.Clients.All.releaseNotification(notification);

            return TaskHelper.Completed();
        }

        private Task OnSystemNotificationAsync(SystemNotification notification, CancellationToken cancellationToken = default(CancellationToken)) {
            HubContext.Clients.All.systemNotification(notification);

            return TaskHelper.Completed();
        }

        private IHubContext<IMessageBusHubClientMethods> _hubContext;
        private IHubContext<IMessageBusHubClientMethods> HubContext => _hubContext ?? (_hubContext = _connectionManager.GetHubContext<MessageBusHub, IMessageBusHubClientMethods>());
    }
}