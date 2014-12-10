using System;
using Exceptionless.Core.Caching;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    public class DefaultSessionIdPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private static TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

        public DefaultSessionIdPlugin(ICacheClient cacheClient) {
            _cacheClient = cacheClient;
        }

        public override void EventProcessing(EventContext context) {
            var user = context.Event.GetUserIdentity();
            if (user == null || String.IsNullOrEmpty(user.Identity))
                return;

            string cacheKey = "id-session:" + user.Identity;
            var sessionId = context.Event.Type != Event.KnownTypes.SessionStart ? _cacheClient.Get<string>(cacheKey) : null;
            if (sessionId == null) {
                sessionId = Guid.NewGuid().ToString("N");
                _cacheClient.Set(cacheKey, sessionId, _sessionTimeout);
            } else {
                _cacheClient.SetExpiration(cacheKey, _sessionTimeout);
            }

            context.Event.SessionId = sessionId;
        }
    }
}
