using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
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

        public override async Task EventProcessingAsync(EventContext context) {
            var user = context.Event.GetUserIdentity();
            if (String.IsNullOrEmpty(user?.Identity) || !String.IsNullOrEmpty(context.Event.SessionId))
                return;

            string cacheKey = $"session:{context.Event.ProjectId}:{user.Identity}";
            var sessionId = context.Event.Type != Event.KnownTypes.SessionStart ? await _cacheClient.GetAsync<string>(cacheKey).AnyContext() : null;
            if (sessionId == null) {
                sessionId = Guid.NewGuid().ToString("N");
                await _cacheClient.SetAsync(cacheKey, sessionId, _sessionTimeout).AnyContext();
            } else {
                await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
            }

            context.Event.SessionId = sessionId;

            if (context.Event.Type == Event.KnownTypes.SessionEnd)
                await _cacheClient.RemoveAsync(cacheKey).AnyContext();
        }
    }
}
