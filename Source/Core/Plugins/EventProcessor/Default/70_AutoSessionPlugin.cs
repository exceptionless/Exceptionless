using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Utility;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(70)]
    public class AutoSessionPlugin : EventProcessorPluginBase {
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromDays(1);
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly UpdateStatsAction _updateStats;
        private readonly AssignToStackAction _assignToStack;

        public AutoSessionPlugin(ICacheClient cacheClient, IEventRepository eventRepository, AssignToStackAction assignToStack, UpdateStatsAction updateStats) {
            _cacheClient = new ScopedCacheClient(cacheClient, "session");
            _eventRepository = eventRepository;
            _assignToStack = assignToStack;
            _updateStats = updateStats;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var identityGroups = contexts.Where(c => String.IsNullOrEmpty(c.Event.SessionId) && c.Event.GetUserIdentity()?.Identity != null)
                .OrderBy(c => c.Event.Date)
                .GroupBy(c => c.Event.GetUserIdentity().Identity);
            
            foreach (var identityGroup in identityGroups) {
                string cacheKey = $"{identityGroup.First().Project.Id}:identity:{identityGroup.Key.ToSHA1()}";
                string sessionId = null;
                
                var sessionsToUpdate = new Dictionary<string, EventContext>();
                foreach (var context in identityGroup) {
                    if (String.IsNullOrEmpty(sessionId) && !context.Event.IsSessionStart()) {
                        sessionId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
                        if (!String.IsNullOrEmpty(sessionId))
                            await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
                    }

                    if (context.Event.IsSessionStart() || String.IsNullOrEmpty(sessionId)) {
                        if (context.Event.IsSessionStart() && !String.IsNullOrEmpty(sessionId)) {
                            await CreateSessionEndEventAsync(context, sessionId).AnyContext();
                            await UpdateSessionStartEventAsync(context, sessionId, isSessionEnd: true);
                        }

                        sessionId = context.Event.SessionId = ObjectId.GenerateNewId(context.Event.Date.DateTime).ToString();
                        await _cacheClient.SetAsync(cacheKey, sessionId, _sessionTimeout).AnyContext();

                        if (!context.Event.IsSessionStart()) {
                            var lastContext = GetLastActivity(identityGroup.Where(c => c.Event.Date.Ticks > context.Event.Date.Ticks).ToList());
                            string sessionStartEventId = await CreateSessionStartEventAsync(context, lastContext?.Event.Date.UtcDateTime, lastContext?.Event.IsSessionEnd()).AnyContext();

                            if (lastContext == null || !lastContext.Event.IsSessionEnd())
                                await _cacheClient.SetAsync($"{context.Project.Id}:start:{sessionId}", sessionStartEventId).AnyContext();
                        }
                    } else {
                        context.Event.SessionId = sessionId;
                        sessionsToUpdate[sessionId] = context;
                    }

                    if (context.Event.IsSessionEnd()) {
                        sessionId = null;
                        await _cacheClient.RemoveAsync(cacheKey).AnyContext();
                    }
                }

                foreach (var pair in sessionsToUpdate) {
                    await UpdateSessionStartEventAsync(pair.Value, pair.Key, pair.Value.Event.IsSessionEnd());
                }
            }
        }
        
        private EventContext GetLastActivity(IList<EventContext> contexts) {
            EventContext lastContext = null;
            foreach (var context in contexts) {
                if (context.Event.IsSessionEnd())
                    return context;

                if (context.Event.IsSessionStart())
                    break;

                lastContext = context;
            }

            return lastContext;
        }

        private async Task<string> CreateSessionStartEventAsync(EventContext context, DateTime? lastActivityUtc, bool? isSessionEnd) {
            var startEvent = context.Event.ToSessionStartEvent(lastActivityUtc, isSessionEnd, context.Organization.HasPremiumFeatures);
            
            var startEventContexts = new List<EventContext> {
                new EventContext(startEvent) { Project = context.Project, Organization = context.Organization }
            };

            await _assignToStack.ProcessBatchAsync(startEventContexts).AnyContext();
            await _updateStats.ProcessBatchAsync(startEventContexts).AnyContext();
            await _eventRepository.AddAsync(startEvent).AnyContext();

            return startEvent.Id;
        }

        private async Task<string> CreateSessionEndEventAsync(EventContext context, string sessionId) {
            var endEvent = context.Event.ToSessionEndEvent(sessionId);

            var endEventContexts = new List<EventContext> {
                new EventContext(endEvent) { Project = context.Project, Organization = context.Organization }
            };

            await _assignToStack.ProcessBatchAsync(endEventContexts).AnyContext();
            await _updateStats.ProcessBatchAsync(endEventContexts).AnyContext();
            await _eventRepository.AddAsync(endEvent).AnyContext();

            return endEvent.Id;
        }
        
        private async Task<string> UpdateSessionStartEventAsync(EventContext context, string sessionId, bool isSessionEnd = false) {
            string sessionStartEventIdCacheKey = $"{context.Project.Id}:start:{sessionId}";
            string sessionStartEventId = await _cacheClient.GetAsync<string>(sessionStartEventIdCacheKey, null).AnyContext();
            if (sessionStartEventId != null) {
                await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, context.Event.Date.UtcDateTime, isSessionEnd).AnyContext();

                if (isSessionEnd)
                    await _cacheClient.RemoveAsync(sessionStartEventIdCacheKey).AnyContext();
            }

            return sessionStartEventId;
        }
    }
}