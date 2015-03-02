using System;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;
using Foundatio.Caching;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(50)]
    public class DefaultSessionIdPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

        public DefaultSessionIdPlugin(ICacheClient cacheClient) {
            _cacheClient = cacheClient;
        }

        public override void EventProcessing(EventContext context) {
            var user = context.Event.GetUserIdentity();
            if (user == null || String.IsNullOrEmpty(user.Identity) || !String.IsNullOrEmpty(context.Event.SessionId))
                return;

            string cacheKey = String.Format("session:{0}:{1}", context.Event.ProjectId, user.Identity);
            var sessionId = context.Event.Type != Event.KnownTypes.SessionStart ? _cacheClient.Get<string>(cacheKey) : null;
            if (sessionId == null) {
                sessionId = Guid.NewGuid().ToString("N");
                _cacheClient.Set(cacheKey, sessionId, _sessionTimeout);
            } else {
                _cacheClient.SetExpiration(cacheKey, _sessionTimeout);
            }

            context.Event.SessionId = sessionId;

            if (context.Event.Type == Event.KnownTypes.SessionEnd)
                _cacheClient.Remove(cacheKey);
        }
    }
}
