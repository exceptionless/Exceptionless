﻿using System;
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
            var autoSessionEvents = contexts
                .Where(c => c.Event.GetUserIdentity()?.Identity != null
                    && String.IsNullOrEmpty(c.Event.GetSessionId())).ToList();

            var manualSessionsEvents = contexts
                .Where(c => !String.IsNullOrEmpty(c.Event.GetSessionId())).ToList();
            
            await ProcessAutoSessionsAsync(autoSessionEvents).AnyContext();
            await ProcessManualSessionsAsync(manualSessionsEvents).AnyContext();
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
                var sessionStartEvent = session.FirstOrDefault(ev => ev.Event.IsSessionStart());
                
                // sync the session start event with the first session event.
                if (sessionStartEvent != null)
                    sessionStartEvent.Event.Date = firstSessionEvent.Event.Date;

                // cancel duplicate end events (1 per session id)
                session.Where(ev => ev.Event.IsSessionEnd()).Skip(1).ForEach(ev => ev.IsCancelled = true);
                var sessionEndEvent = session.FirstOrDefault(ev => ev.Event.IsSessionEnd());

                // sync the session end event with the last session event.
                if (sessionEndEvent != null)
                    sessionEndEvent.Event.Date = lastSessionEvent.Event.Date;

                // mark the heart beat events as hidden. This will cause new stacks to be marked as hidden, otherwise this value will be reset by the stack.
                session.Where(ev => ev.Event.IsSessionHeartbeat()).ForEach(ctx => ctx.Event.IsHidden = true);

                // try to update an existing session
                var sessionStartEventId = await UpdateSessionStartEventAsync(projectId, session.Key, lastSessionEvent.Event.Date.UtcDateTime, sessionEndEvent != null).AnyContext();

                // do we already have a session start for this session id?
                if (!String.IsNullOrEmpty(sessionStartEventId) && sessionStartEvent != null) {
                    sessionStartEvent.IsCancelled = true;
                } else if (String.IsNullOrEmpty(sessionStartEventId) && sessionStartEvent != null) {
                    // no existing session, session start is in the batch
                    sessionStartEvent.Event.UpdateSessionStart(lastSessionEvent.Event.Date.UtcDateTime, sessionEndEvent != null);
                    sessionStartEvent.SetProperty("SetSessionStartEventId", true);
                } else if (String.IsNullOrEmpty(sessionStartEventId)) {
                    // no session start event found and none in the batch

                    // if session end, without any session events, cancel
                    if (session.Count(s => !s.IsCancelled) == 1 && firstSessionEvent.Event.IsSessionEnd()) {
                        firstSessionEvent.IsCancelled = true;
                        continue;
                    }

                    // create a new session start event
                    await CreateSessionStartEventAsync(firstSessionEvent, lastSessionEvent.Event.Date.UtcDateTime, sessionEndEvent != null).AnyContext();
                }
            }
        }

        private async Task ProcessAutoSessionsAsync(ICollection<EventContext> contexts) {
            var identityGroups = contexts
                .OrderBy(c => c.Event.Date)
                .GroupBy(c => c.Event.GetUserIdentity()?.Identity);

            foreach (var identityGroup in identityGroups) {
                string projectId = identityGroup.First().Project.Id;

                // group events into sessions (split by session ends)
                foreach (var session in CreateSessionGroups(identityGroup)) {
                    bool isNewSession = false;
                    var firstSessionEvent = session.First();
                    var lastSessionEvent = session.Last();

                    // cancel duplicate start events
                    session.Where(ev => ev.Event.IsSessionStart()).Skip(1).ForEach(ev => ev.IsCancelled = true);
                    var sessionStartEvent = session.FirstOrDefault(ev => ev.Event.IsSessionStart());
                    
                    // sync the session start event with the first session event.
                    if (sessionStartEvent != null)
                        sessionStartEvent.Event.Date = firstSessionEvent.Event.Date;
                    
                    // mark the heart beat events as hidden. This will cause new stacks to be marked as hidden, otherwise this value will be reset by the stack.
                    session.Where(ev => ev.Event.IsSessionHeartbeat()).ForEach(ctx => ctx.Event.IsHidden = true);

                    string sessionId = await GetIdentitySessionIdAsync(projectId, identityGroup.Key).AnyContext();

                    // if session end, without any session events, cancel
                    if (String.IsNullOrEmpty(sessionId) && session.Count == 1 && firstSessionEvent.Event.IsSessionEnd()) {
                        firstSessionEvent.IsCancelled = true;
                        continue;
                    }

                    // no existing session, create a new one
                    if (String.IsNullOrEmpty(sessionId)) {
                        sessionId = ObjectId.GenerateNewId(firstSessionEvent.Event.Date.DateTime).ToString();
                        isNewSession = true;
                    }

                    session.ForEach(s => s.Event.SetSessionId(sessionId));

                    if (isNewSession) {
                        if (sessionStartEvent != null) {
                            sessionStartEvent.Event.UpdateSessionStart(lastSessionEvent.Event.Date.UtcDateTime, lastSessionEvent.Event.IsSessionEnd());
                            sessionStartEvent.SetProperty("SetSessionStartEventId", true);
                        } else {
                            await CreateSessionStartEventAsync(firstSessionEvent, lastSessionEvent.Event.Date.UtcDateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                        }

                        if (!lastSessionEvent.IsCancelled && !lastSessionEvent.Event.IsSessionEnd())
                            await SetIdentitySessionIdAsync(projectId, identityGroup.Key, sessionId).AnyContext();
                    } else {
                        // we already have a session start, cancel this one
                        if (sessionStartEvent != null)
                            sessionStartEvent.IsCancelled = true;

                        await UpdateSessionStartEventAsync(projectId, sessionId, lastSessionEvent.Event.Date.UtcDateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                    }
                }
            }
        }

        public override async Task EventProcessedAsync(EventContext context) {
            if (context.GetProperty("SetSessionStartEventId") != null)
                await SetSessionStartEventIdAsync(context.Project.Id, context.Event.GetSessionId(), context.Event.Id).AnyContext();

            await base.EventProcessedAsync(context);
        }

        private static List<List<EventContext>> CreateSessionGroups(IGrouping<String, EventContext> identityGroup) {
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