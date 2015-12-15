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
                // Deduplicate session start and end events.
                sessionGroup.Where(c => c.Event.IsSessionStart()).Skip(1).ForEach(c => c.IsCancelled = true);
                sessionGroup.OrderByDescending(c => c.Event.Date).Where(c => c.Event.IsSessionEnd()).Skip(1).ForEach(c => c.IsCancelled = true);

                var oldestContext = sessionGroup.First();
                string cacheKey = $"{oldestContext.Project.Id}:start:{oldestContext.Event.SessionId}";

                string sessionStartEventId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
                if (!String.IsNullOrEmpty(sessionStartEventId))
                    await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
                
                EventContext sessionStartContext = null;
                foreach (var context in sessionGroup.Where(c => !c.IsCancelled)) {
                    if (context.Event.IsSessionStart()) {
                        if (!String.IsNullOrEmpty(sessionStartEventId)) {
                            // Session start event already exists.
                            context.IsCancelled = true;
                            continue;
                        }

                        sessionStartContext = context;
                        context.SetProperty(CREATE_SESSION_START_CACHE_ENTRY, true);
                    }

                    if (context.Event.IsSessionEnd()) {
                        sessionStartContext?.RemoveProperty(CREATE_SESSION_START_CACHE_ENTRY);

                        if (!String.IsNullOrEmpty(sessionStartEventId)) {
                            await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, context.Event.Date.UtcDateTime, true).AnyContext();
                            await _cacheClient.RemoveAsync(cacheKey).AnyContext();
                        } else {
                            if (sessionStartContext != null) {
                                sessionStartContext.Event.UpdateSessionStart(context.Event.Date.UtcDateTime, true);
                            } else {
                                await CreateSessionStartEventAsync(oldestContext, context.Event.Date.UtcDateTime, true);
                            }
                        }
                    }
                }
            }
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

        private async Task<string> CreateSessionStartEventAsync(EventContext context, DateTime lastActivityUtc, bool isSessionEnd) {
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

            startEvent.UpdateSessionStart(lastActivityUtc, isSessionEnd);
            
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