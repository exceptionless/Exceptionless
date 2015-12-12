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

                var sessionStartId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
                if (sessionStartId == null)
                    continue;
                
                var sessionStartEvent = await _eventRepository.GetByIdAsync(sessionStartId).AnyContext();
                if (sessionStartEvent == null) {
                    await _cacheClient.RemoveAsync(cacheKey).AnyContext();
                    continue;
                }
                
                await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
                sessionStartEvent.Value = (decimal)(newestContext.Event.Date - sessionStartEvent.Date).TotalSeconds;

                if (newestContext.Event.IsSessionEnd()) {
                    // Store session end time or that it's completed
                }

                // TODO: Make this an update script instead of getting and setting it.
                // TODO: Put this in a batch on a timer to save so it's more efficient.
                await _eventRepository.SaveAsync(sessionStartEvent).AnyContext();
            }

            // keep track of last activity and periodically save.
            // potentially create session ends.
        }
    }
}