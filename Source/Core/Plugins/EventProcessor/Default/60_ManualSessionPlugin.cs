using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(60)]
    public class ManualSessionPlugin : EventProcessorPluginBase {
        const string CREATE_SESSION_START_CACHE_ENTRY = "CREATE_SESSION_START_CACHE_ENTRY";

        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromDays(1);
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly UpdateStatsAction _updateStats;
        private readonly AssignToStackAction _assignToStack;

        public ManualSessionPlugin(ICacheClient cacheClient, IEventRepository eventRepository, AssignToStackAction assignToStack, UpdateStatsAction updateStats) {
            _cacheClient = new ScopedCacheClient(cacheClient, "session");
            _eventRepository = eventRepository;
            _assignToStack = assignToStack;
            _updateStats = updateStats;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var sessionGroups = contexts.Where(c => !String.IsNullOrEmpty(c.Event.SessionId))
                .OrderBy(c => c.Event.Date)
                .GroupBy(c => c.Event.SessionId);
        
            foreach (var sessionGroup in sessionGroups) {
                var oldestContext = sessionGroup.First();
                string cacheKey = $"{oldestContext.Project.Id}:start:{oldestContext.Event.SessionId}";

                string sessionStartEventId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
                if (!String.IsNullOrEmpty(sessionStartEventId))
                    await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();

                // Deduplicate session start and end events.
                sessionGroup.Where(c => c.Event.IsSessionStart()).Skip(String.IsNullOrEmpty(sessionStartEventId) ? 1 : 0).ForEach(c => c.IsCancelled = true);
                sessionGroup.OrderByDescending(c => c.Event.Date).Where(c => c.Event.IsSessionEnd()).Skip(1).ForEach(c => c.IsCancelled = true);

                var validContexts = sessionGroup.Where(c => !c.IsCancelled).ToList();
                if (validContexts.Count == 0)
                    continue;

                oldestContext = validContexts.First();
                var newestContext = validContexts.Last();

                var sessionStartEventContext = validContexts.FirstOrDefault(c => c.Event.IsSessionStart());
                UpdateEventContextDate(sessionStartEventContext, oldestContext.Event.Date);
                
                var sessionEndEventContext = validContexts.FirstOrDefault(c => c.Event.IsSessionEnd());
                UpdateEventContextDate(sessionEndEventContext, newestContext.Event.Date);
                
                // Update or create the session start event.
                var createSessionStartEvent = String.IsNullOrEmpty(sessionStartEventId) && sessionStartEventContext == null;
                if (createSessionStartEvent) {
                    string sessionStartId = await CreateSessionStartEventAsync(oldestContext, validContexts.Count > 1 ? newestContext.Event.Date.UtcDateTime : (DateTime?)null, sessionEndEventContext != null).AnyContext();
                    if (sessionEndEventContext == null)
                        await _cacheClient.SetAsync(cacheKey, sessionStartId).AnyContext();
                } else if (sessionStartEventContext != null) {
                    if (validContexts.Count > 1)
                        sessionStartEventContext.Event.UpdateSessionStart(newestContext.Event.Date.UtcDateTime, sessionEndEventContext != null);
                    
                    if (sessionEndEventContext == null)
                        sessionStartEventContext.SetProperty(CREATE_SESSION_START_CACHE_ENTRY, true);
                } else if (sessionEndEventContext != null) {
                    await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, sessionEndEventContext.Event.Date.UtcDateTime, true).AnyContext();
                    await _cacheClient.RemoveAsync(cacheKey).AnyContext();
                }
            }
        }

        private void UpdateEventContextDate(EventContext context, DateTimeOffset date) {
            if (context == null)
                return;

            context.Event.Date = date;
        }

        public override async Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            var sessionGroups = contexts.Where(c => !String.IsNullOrEmpty(c.Event.SessionId) && c.Event.IsSessionStart()).GroupBy(c => c.Event.SessionId);
            foreach (var sessionGroup in sessionGroups) {
                foreach (var context in sessionGroup) {
                    if (!context.HasProperty(CREATE_SESSION_START_CACHE_ENTRY))
                        continue;

                    string cacheKey = $"{context.Project.Id}:start:{sessionGroup.Key}";
                    await _cacheClient.SetAsync(cacheKey, context.Event.Id, _sessionTimeout).AnyContext();
                }
            }
        }

        private async Task<string> CreateSessionStartEventAsync(EventContext context, DateTime? lastActivityUtc, bool? isSessionEnd) {
            // TODO: Be selective about what data we copy.
            var startEvent = new PersistentEvent {
                SessionId = context.Event.SessionId,
                Data = context.Event.Data,
                Date = context.Event.Date,
                Geo = context.Event.Geo,
                OrganizationId = context.Event.OrganizationId,
                ProjectId = context.Event.ProjectId,
                Tags = context.Event.Tags,
                Type = Event.KnownTypes.SessionStart
            };
            
            if (lastActivityUtc.HasValue)
                startEvent.UpdateSessionStart(lastActivityUtc.Value, isSessionEnd.GetValueOrDefault());
            else
                startEvent.CopyDataToIndex(!context.Organization.HasPremiumFeatures ? Event.KnownDataKeys.SessionEnd : null);

            var startEventContexts = new List<EventContext> {
                new EventContext(startEvent) { Project = context.Project, Organization = context.Organization }
            };

            await _assignToStack.ProcessBatchAsync(startEventContexts).AnyContext();
            await _updateStats.ProcessBatchAsync(startEventContexts).AnyContext();
            await _eventRepository.AddAsync(startEvent).AnyContext();

            return startEvent.Id;
        }
    }
}