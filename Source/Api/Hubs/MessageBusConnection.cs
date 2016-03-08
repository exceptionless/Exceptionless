using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;
using Microsoft.AspNet.SignalR;

namespace Exceptionless.Api.Hubs {
    public class MessageBusConnection : PersistentConnection {
        private readonly ConnectionMapping _userIdConnections;
        private readonly ILogger _logger;

        public MessageBusConnection(ConnectionMapping userIdConnections, ILogger<MessageBusConnection> logger) {
            _userIdConnections = userIdConnections;
            _logger = logger;
        }
        
        protected override async Task OnConnected(IRequest request, string connectionId) {
            try {
                foreach (var organizationId in request.User.GetOrganizationIds())
                    await Groups.Add(connectionId, organizationId);

                _userIdConnections.Add(request.User.GetUserId(), connectionId);
            } catch (Exception ex) {
                _logger.Error(ex, "OnConnected Error: {0}", ex.Message);
                throw;
            }
        }

        protected override Task OnDisconnected(IRequest request, string connectionId, bool stopCalled) {
            try {
                _userIdConnections.Remove(request.User.GetUserId(), connectionId);
            } catch (Exception ex) {
                _logger.Error(ex, "OnDisconnected Error: {0}", ex.Message);
                throw;
            }

            return Task.CompletedTask;
        }

        protected override async Task OnReconnected(IRequest request, string connectionId) {
            try {
                foreach (var organizationId in request.User.GetOrganizationIds())
                    await Groups.Add(connectionId, organizationId);

                if (!_userIdConnections.GetConnections(request.User.GetUserId()).Contains(connectionId))
                    _userIdConnections.Add(request.User.GetUserId(), connectionId);
            } catch (Exception ex) {
                _logger.Error(ex, "OnReconnected Error: {0}", ex.Message);
                throw;
            }
        }

        protected override bool AuthorizeRequest(IRequest request) {
            return request.User.Identity.IsAuthenticated;
        }
    }
}