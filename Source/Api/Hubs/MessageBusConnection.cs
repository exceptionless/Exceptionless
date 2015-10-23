using System;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;
using Microsoft.AspNet.SignalR;

namespace Exceptionless.Api.Hubs {
    public class MessageBusConnection : PersistentConnection {
        private readonly ConnectionMapping _userIdConnections;

        public MessageBusConnection(ConnectionMapping userIdConnections) {
            _userIdConnections = userIdConnections;
        }
        
        protected override Task OnConnected(IRequest request, string connectionId) {
            foreach (string organizationId in request.User.GetOrganizationIds())
                Groups.Add(connectionId, organizationId);

            _userIdConnections.Add(request.User.GetUserId(), connectionId);

            return TaskHelper.Completed();
        }

        protected override Task OnDisconnected(IRequest request, string connectionId, bool stopCalled) {
            _userIdConnections.Remove(request.User.GetUserId(), connectionId);

            return TaskHelper.Completed();
        }

        protected override Task OnReconnected(IRequest request, string connectionId) {
            foreach (string organizationId in request.User.GetOrganizationIds())
                Groups.Add(connectionId, organizationId);

            if (!_userIdConnections.GetConnections(request.User.GetUserId()).Contains(connectionId))
                _userIdConnections.Add(request.User.GetUserId(), connectionId);

            return TaskHelper.Completed();
        }

        protected override bool AuthorizeRequest(IRequest request) {
            return request.User.Identity.IsAuthenticated;
        }
    }
}