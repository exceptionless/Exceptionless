using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(70)]
    public class UpdateSessionPlugin : EventProcessorPluginBase {
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromDays(1);
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;

        public UpdateSessionPlugin(ICacheClient cacheClient, IEventRepository eventRepository) {
            _cacheClient = new ScopedCacheClient(cacheClient, "session");
            _eventRepository = eventRepository;
        }

        public override async Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            var sessionGroups = contexts.Where(c => !String.IsNullOrEmpty(c.Event.SessionId)).OrderByDescending(c => c.Event.Date).GroupBy(c => c.Event.SessionId);
            foreach (var sessionGroup in sessionGroups) {
                var newestContext = sessionGroup.First();
                string cacheKey = $"{newestContext.Project.Id}:start:{newestContext.Event.SessionId}";

                var sessionStartEventId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
                if (sessionStartEventId == null)
                    continue;
                
                // TODO: Put this in a batch on a timer to save so it's more efficient.
                var updated = await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, newestContext.Event.Date.UtcDateTime, newestContext.Event.IsSessionEnd()).AnyContext();
                if (updated)
                    await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
                else
                    await _cacheClient.RemoveAsync(cacheKey).AnyContext();
            }

            // TODO: keep track of last activity and periodically save.
            // TODO: potentially create session ends.
        }
    }
}