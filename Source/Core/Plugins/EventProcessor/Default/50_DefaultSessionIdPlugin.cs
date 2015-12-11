using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Utility;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(50)]
    public class AutoSessionPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

        public AutoSessionPlugin(ICacheClient cacheClient) {
            _cacheClient = new ScopedCacheClient(cacheClient, "session");
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var identityGroups = contexts.Where(c => c.Event.GetUserIdentity()?.Identity != null)
                .OrderBy(c => c.Event.CreatedUtc)
                .GroupBy(c => c.Event.GetUserIdentity().Identity);

            foreach (var identityGroup in identityGroups) {
                string sessionId = null;

                foreach (var context in identityGroup.Where(c => String.IsNullOrEmpty(c.Event.SessionId))) {
                    if (String.IsNullOrEmpty(sessionId) && !context.Event.IsSessionStart()) {
                        sessionId = await _cacheClient.GetAsync<string>($"{context.Project.Id}:{identityGroup.Key}", null).AnyContext();
                    }

                    if (context.Event.IsSessionStart() || String.IsNullOrEmpty(sessionId)) {
                        sessionId = ObjectId.GenerateNewId(context.Event.CreatedUtc).ToString();
                        await _cacheClient.SetAsync($"{context.Project.Id}:{identityGroup.Key}", sessionId, _sessionTimeout).AnyContext();
                    }
                    
                    context.Event.SessionId = sessionId;

                    if (context.Event.IsSessionEnd()) {
                        sessionId = null;
                        await _cacheClient.RemoveAsync($"{context.Project.Id}:{identityGroup.Key}").AnyContext();
                    }
                }
            }
        }
    }
}