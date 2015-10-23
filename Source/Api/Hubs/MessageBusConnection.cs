using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;
using Microsoft.AspNet.SignalR;

namespace Exceptionless.Api.Hubs {
    public class MessageBusConnection : PersistentConnection {
        private readonly ConnectionMapping _userIdConnections;

        public MessageBusConnection(ConnectionMapping userIdConnections) {
            _userIdConnections = userIdConnections;
        }
        
        protected override async Task OnConnected(IRequest request, string connectionId) {
            try {
                foreach (string organizationId in request.User.GetOrganizationIds())
                    await Groups.Add(connectionId, organizationId);

                _userIdConnections.Add(request.User.GetUserId(), connectionId);
                await base.OnConnected(request, connectionId);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Message($"OnConnected Error: {ex.Message}").Tag("SignalR").Write();
                throw;
            }
        }

        protected override Task OnDisconnected(IRequest request, string connectionId, bool stopCalled) {
            try {
                _userIdConnections.Remove(request.User.GetUserId(), connectionId);

                return base.OnDisconnected(request, connectionId, stopCalled);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Message($"OnDisconnected Error: {ex.Message}").Tag("SignalR").Write();
                throw;
            }
        }

        protected override async Task OnReconnected(IRequest request, string connectionId) {
            try {
                foreach (string organizationId in request.User.GetOrganizationIds())
                    await Groups.Add(connectionId, organizationId);

                if (!_userIdConnections.GetConnections(request.User.GetUserId()).Contains(connectionId))
                    _userIdConnections.Add(request.User.GetUserId(), connectionId);

                await base.OnReconnected(request, connectionId);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Message($"OnReconnected Error: {ex.Message}").Tag("SignalR").Write();
                throw;
            }
        }

        protected override bool AuthorizeRequest(IRequest request) {
            return request.User.Identity.IsAuthenticated;
        }
    }
}