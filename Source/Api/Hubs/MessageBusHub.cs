using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Exceptionless.Api.Hubs {
    [HubName("messages")]
    public class MessageBusHub : Hub<IMessageBusHubClientMethods> {
        private readonly ConnectionMapping _userIdConnections;

        public MessageBusHub(ConnectionMapping userIdConnections) {
            _userIdConnections = userIdConnections;
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