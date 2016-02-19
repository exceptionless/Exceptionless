using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(70)]
    public class SessionPlugin : EventProcessorPluginBase {
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(15);
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly UpdateStatsAction _updateStats;
        private readonly AssignToStackAction _assignToStack;
        private readonly LocationPlugin _locationPlugin;

        public SessionPlugin(ICacheClient cacheClient, IEventRepository eventRepository, AssignToStackAction assignToStack, UpdateStatsAction updateStats, LocationPlugin locationPlugin) {
            _cacheClient = new ScopedCacheClient(cacheClient, "session");
            _eventRepository = eventRepository;
            _assignToStack = assignToStack;
            _updateStats = updateStats;
            _locationPlugin = locationPlugin;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            await ProcessAutoSessionsAsync(contexts
                .Where(c => c.Event.GetUserIdentity()?.Identity != null
                    && String.IsNullOrEmpty(c.Event.GetSessionId())).ToList()).AnyContext();

            await ProcessManualSessionsAsync(contexts
                .Where(c => !String.IsNullOrEmpty(c.Event.GetSessionId())).ToList()).AnyContext();
        }

        private async Task ProcessManualSessionsAsync(ICollection<EventContext> contexts) {
            var sessionIdGroups = contexts
                .OrderBy(c => c.Event.Date)
                .GroupBy(c => c.Event.GetSessionId());

            foreach (var session in sessionIdGroups) {
                string projectId = session.First().Project.Id;

                var firstSessionEvent = session.First();
                var lastSessionEvent = session.Last();

                // cancel duplicate start events (1 per session id)
                session.Where(ev => ev.Event.IsSessionStart()).Skip(1).ForEach(ev => ev.IsCancelled = true);

                // cancel duplicate end events (1 per session id)
                session.Where(ev => ev.Event.IsSessionEnd()).Skip(1).ForEach(ev => ev.IsCancelled = true);

                // cancel heart beat events (we don't save them)
                session.Where(ev => ev.Event.IsSessionHeartbeat()).ForEach(ev => ev.IsCancelled = true);

                var sessionStart = await UpdateSessionStartEventAsync(projectId, session.Key, lastSessionEvent.Event.Date.DateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                if (String.IsNullOrEmpty(sessionStart)) {
                    // if session end, without any session events, cancel
                    if (session.Count() == 1 && firstSessionEvent.Event.IsSessionEnd()) {
                        firstSessionEvent.IsCancelled = true;
                        continue;
                    }

                    await CreateSessionStartEventAsync(firstSessionEvent, lastSessionEvent.Event.Date.DateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                }
            }
        }

        private async Task ProcessAutoSessionsAsync(ICollection<EventContext> contexts) {
            var identityGroups = contexts
                .OrderBy(c => c.Event.Date)
                .GroupBy(c => c.Event.GetUserIdentity()?.Identity);

            foreach (var identityGroup in identityGroups) {
                string projectId = identityGroup.First().Project.Id;

                foreach (var session in CreateSessionGroups(identityGroup)) {
                    bool isNewSession = false;
                    var firstSessionEvent = session.First();
                    var lastSessionEvent = session.Last();

                    // cancel duplicate start events
                    session.Where(ev => ev.Event.IsSessionStart()).Skip(1).ForEach(ev => ev.IsCancelled = true);

                    // cancel heart beat events (we don't save them)
                    session.Where(ev => ev.Event.IsSessionHeartbeat()).ForEach(ev => ev.IsCancelled = true);

                    string sessionId = await GetIdentitySessionIdAsync(identityGroup.Key, projectId).AnyContext();

                    // if session end, without any session events, cancel
                    if (String.IsNullOrEmpty(sessionId) && session.Count == 1 && firstSessionEvent.Event.IsSessionEnd()) {
                        firstSessionEvent.IsCancelled = true;
                        continue;
                    }

                    if (String.IsNullOrEmpty(sessionId)) {
                        sessionId = ObjectId.GenerateNewId(firstSessionEvent.Event.Date.DateTime).ToString();
                        isNewSession = true;
                    }

                    session.ForEach(s => s.Event.SetSessionId(sessionId));

                    if (isNewSession) {
                        await CreateSessionStartEventAsync(firstSessionEvent, lastSessionEvent.Event.Date.UtcDateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                        await SetIdentitySessionIdAsync(projectId, identityGroup.Key, sessionId).AnyContext();
                    } else
                        await UpdateSessionStartEventAsync(projectId, sessionId, lastSessionEvent.Event.Date.DateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                }
            }
        }

        private static List<List<EventContext>> CreateSessionGroups(IGrouping<String, EventContext> identityGroup) {
            // group events into sessions (split by session ends)
            var sessions = new List<List<EventContext>>();
            var currentSession = new List<EventContext>();
            sessions.Add(currentSession);
            foreach (var context in identityGroup) {
                currentSession.Add(context);

                // start new session, after session end
                if (context.Event.IsSessionEnd()) {
                    currentSession = new List<EventContext>();
                    sessions.Add(currentSession);
                }
            }

            // remove empty sessions
            sessions = sessions.Where(s => s.Count > 0).ToList();
            return sessions;
        }

        private string GetSessionStartEventIdCacheKey(string projectId, string sessionId) {
            return String.Concat(projectId, ":start:", sessionId);
        }

        private async Task<string> GetSessionStartEventIdAsync(string projectId, string sessionId) {
            string cacheKey = GetSessionStartEventIdCacheKey(projectId, sessionId);
            string eventId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
            if (!String.IsNullOrEmpty(eventId))
                await _cacheClient.SetExpirationAsync(cacheKey, TimeSpan.FromDays(1)).AnyContext();

            return eventId;
        }

        private async Task SetSessionStartEventIdAsync(string projectId, string sessionId, string eventId) {
            await _cacheClient.SetAsync<string>(GetSessionStartEventIdCacheKey(projectId, sessionId), eventId, TimeSpan.FromDays(1)).AnyContext();
        }

        private string GetIdentitySessionIdCacheKey(string projectId, string identity) {
            return String.Concat(projectId, ":identity:", identity.ToSHA1());
        }

        private async Task<string> GetIdentitySessionIdAsync(string projectId, string identity) {
            string cacheKey = GetIdentitySessionIdCacheKey(projectId, identity);
            string sessionId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
            if (!String.IsNullOrEmpty(sessionId)) {
                await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
                await _cacheClient.SetExpirationAsync(GetSessionStartEventIdCacheKey(projectId, sessionId), TimeSpan.FromDays(1)).AnyContext();
            }

            return sessionId;
        }

        private async Task SetIdentitySessionIdAsync(string projectId, string identity, string sessionId) {
            await _cacheClient.SetAsync<string>(GetIdentitySessionIdCacheKey(projectId, identity), sessionId, _sessionTimeout).AnyContext();
        }

        private async Task<PersistentEvent> CreateSessionStartEventAsync(EventContext startContext, DateTime? lastActivityUtc, bool? isSessionEnd) {
            var startEvent = startContext.Event.ToSessionStartEvent(lastActivityUtc, isSessionEnd, startContext.Organization.HasPremiumFeatures);
            
            var startEventContexts = new List<EventContext> {
                new EventContext(startEvent) { Project = startContext.Project, Organization = startContext.Organization }
            };

            await _assignToStack.ProcessBatchAsync(startEventContexts).AnyContext();
            await _updateStats.ProcessBatchAsync(startEventContexts).AnyContext();
            await _eventRepository.AddAsync(startEvent).AnyContext();
            await _locationPlugin.EventBatchProcessedAsync(startEventContexts).AnyContext();

            await SetSessionStartEventIdAsync(startContext.Project.Id, startContext.Event.GetSessionId(), startEvent.Id).AnyContext();

            return startEvent;
        }
        
        private async Task<string> UpdateSessionStartEventAsync(string projectId, string sessionId, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false) {
            string sessionStartEventId = await GetSessionStartEventIdAsync(projectId, sessionId).AnyContext();
            if (!String.IsNullOrEmpty(sessionStartEventId)) {
                await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, lastActivityUtc, isSessionEnd, hasError).AnyContext();

                if (isSessionEnd)
                    await _cacheClient.RemoveAsync(GetSessionStartEventIdCacheKey(projectId, sessionId)).AnyContext();
            }

            return sessionStartEventId;
        }
    }
}