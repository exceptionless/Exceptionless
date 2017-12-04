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
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(70)]
    public sealed class SessionPlugin : EventProcessorPluginBase {
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(15);
        private readonly ICacheClient _cache;
        private readonly IEventRepository _eventRepository;
        private readonly UpdateStatsAction _updateStats;
        private readonly AssignToStackAction _assignToStack;
        private readonly LocationPlugin _locationPlugin;

        public SessionPlugin(ICacheClient cacheClient, IEventRepository eventRepository, AssignToStackAction assignToStack, UpdateStatsAction updateStats, LocationPlugin locationPlugin, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _cache = new ScopedCacheClient(cacheClient, "session");
            _eventRepository = eventRepository;
            _assignToStack = assignToStack;
            _updateStats = updateStats;
            _locationPlugin = locationPlugin;
        }

        public override Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var autoSessionEvents = contexts.Where(c => !String.IsNullOrWhiteSpace(c.Event.GetUserIdentity()?.Identity) && String.IsNullOrEmpty(c.Event.GetSessionId())).ToList();
            var manualSessionsEvents = contexts.Where(c => !String.IsNullOrEmpty(c.Event.GetSessionId())).ToList();

            return Task.WhenAll(
                ProcessAutoSessionsAsync(autoSessionEvents), 
                ProcessManualSessionsAsync(manualSessionsEvents)
            );
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
                session.Where(ev => ev.Event.IsSessionStart()).Skip(1).ForEach(ev => {
                    _logger.LogWarning("Discarding duplicate session start events.");
                    ev.IsCancelled = true;
                });
                var sessionStartEvent = session.FirstOrDefault(ev => ev.Event.IsSessionStart());

                // sync the session start event with the first session event.
                if (sessionStartEvent != null)
                    sessionStartEvent.Event.Date = firstSessionEvent.Event.Date;

                // cancel duplicate end events (1 per session id)
                session.Where(ev => ev.Event.IsSessionEnd()).Skip(1).ForEach(ev => {
                    _logger.LogWarning("Discarding duplicate session end events.");
                    ev.IsCancelled = true;
                });
                var sessionEndEvent = session.FirstOrDefault(ev => ev.Event.IsSessionEnd());

                // sync the session end event with the last session event.
                if (sessionEndEvent != null)
                    sessionEndEvent.Event.Date = lastSessionEvent.Event.Date;

                // mark the heart beat events as hidden. This will cause new stacks to be marked as hidden, otherwise this value will be reset by the stack.
                session.Where(ev => ev.Event.IsSessionHeartbeat()).ForEach(ctx => ctx.Event.IsHidden = true);

                // try to update an existing session
                string sessionStartEventId = await UpdateSessionStartEventAsync(projectId, session.Key, lastSessionEvent.Event.Date.UtcDateTime, sessionEndEvent != null).AnyContext();

                // do we already have a session start for this session id?
                if (!String.IsNullOrEmpty(sessionStartEventId) && sessionStartEvent != null) {
                    _logger.LogWarning("Discarding duplicate session start event for session: {SessionStartEventId}", sessionStartEventId);
                    sessionStartEvent.IsCancelled = true;
                } else if (String.IsNullOrEmpty(sessionStartEventId) && sessionStartEvent != null) {
                    // no existing session, session start is in the batch
                    sessionStartEvent.Event.UpdateSessionStart(lastSessionEvent.Event.Date.UtcDateTime, sessionEndEvent != null);
                    sessionStartEvent.SetProperty("SetSessionStartEventId", true);
                } else if (String.IsNullOrEmpty(sessionStartEventId)) {
                    // no session start event found and none in the batch

                    // if session end, without any session events, cancel
                    if (session.Count(s => !s.IsCancelled) == 1 && firstSessionEvent.Event.IsSessionEnd()) {
                        _logger.LogWarning("Discarding session end event with no session events.");
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
                .GroupBy(c => c.Event.GetUserIdentity().Identity);

            foreach (var identityGroup in identityGroups) {
                string projectId = identityGroup.First().Project.Id;

                // group events into sessions (split by session ends)
                foreach (var session in CreateSessionGroups(identityGroup)) {
                    bool isNewSession = false;
                    var firstSessionEvent = session.First();
                    var lastSessionEvent = session.Last();

                    // cancel duplicate start events
                    session.Where(ev => ev.Event.IsSessionStart()).Skip(1).ForEach(ev => {
                        _logger.LogWarning("Discarding duplicate session start events.");
                        ev.IsCancelled = true;
                    });
                    var sessionStartEvent = session.FirstOrDefault(ev => ev.Event.IsSessionStart());

                    // sync the session start event with the first session event.
                    if (sessionStartEvent != null)
                        sessionStartEvent.Event.Date = firstSessionEvent.Event.Date;

                    // mark the heart beat events as hidden. This will cause new stacks to be marked as hidden, otherwise this value will be reset by the stack.
                    session.Where(ev => ev.Event.IsSessionHeartbeat()).ForEach(ctx => ctx.Event.IsHidden = true);

                    string sessionId = await GetIdentitySessionIdAsync(projectId, identityGroup.Key).AnyContext();

                    // if session end, without any session events, cancel
                    if (String.IsNullOrEmpty(sessionId) && session.Count == 1 && firstSessionEvent.Event.IsSessionEnd()) {
                        _logger.LogWarning("Discarding session end event with no session events.");
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
                        if (sessionStartEvent != null) {
                            _logger.LogWarning("Discarding duplicate session start event.");
                            sessionStartEvent.IsCancelled = true;
                        }

                        await UpdateSessionStartEventAsync(projectId, sessionId, lastSessionEvent.Event.Date.UtcDateTime, lastSessionEvent.Event.IsSessionEnd()).AnyContext();
                    }
                }
            }
        }

        public override Task EventProcessedAsync(EventContext context) {
            if (context.GetProperty("SetSessionStartEventId") != null)
                return SetSessionStartEventIdAsync(context.Project.Id, context.Event.GetSessionId(), context.Event.Id);

            return Task.CompletedTask;
        }

        private static List<List<EventContext>> CreateSessionGroups(IGrouping<string, EventContext> identityGroup) {
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
            string eventId = await _cache.GetAsync<string>(cacheKey, null).AnyContext();
            if (!String.IsNullOrEmpty(eventId))
                await _cache.SetExpirationAsync(cacheKey, TimeSpan.FromDays(1)).AnyContext();

            return eventId;
        }

        private Task SetSessionStartEventIdAsync(string projectId, string sessionId, string eventId) {
            return _cache.SetAsync<string>(GetSessionStartEventIdCacheKey(projectId, sessionId), eventId, TimeSpan.FromDays(1));
        }

        private string GetIdentitySessionIdCacheKey(string projectId, string identity) {
            return String.Concat(projectId, ":identity:", identity.ToSHA1());
        }

        private async Task<string> GetIdentitySessionIdAsync(string projectId, string identity) {
            string cacheKey = GetIdentitySessionIdCacheKey(projectId, identity);
            string sessionId = await _cache.GetAsync<string>(cacheKey, null).AnyContext();
            if (!String.IsNullOrEmpty(sessionId)) {
                await Task.WhenAll(
                    _cache.SetExpirationAsync(cacheKey, _sessionTimeout),
                    _cache.SetExpirationAsync(GetSessionStartEventIdCacheKey(projectId, sessionId), TimeSpan.FromDays(1))
                ).AnyContext();
            }

            return sessionId;
        }

        private Task SetIdentitySessionIdAsync(string projectId, string identity, string sessionId) {
            return _cache.SetAsync<string>(GetIdentitySessionIdCacheKey(projectId, identity), sessionId, _sessionTimeout);
        }

        private async Task<PersistentEvent> CreateSessionStartEventAsync(EventContext startContext, DateTime? lastActivityUtc, bool? isSessionEnd) {
            var startEvent = startContext.Event.ToSessionStartEvent(lastActivityUtc, isSessionEnd, startContext.Organization.HasPremiumFeatures);
            var startEventContexts = new List<EventContext> {
                new EventContext(startEvent, startContext.Organization, startContext.Project)
            };

            if (_assignToStack.Enabled)
                await _assignToStack.ProcessBatchAsync(startEventContexts).AnyContext();
            if (_updateStats.Enabled)
                await _updateStats.ProcessBatchAsync(startEventContexts).AnyContext();
            await _eventRepository.AddAsync(startEvent).AnyContext();
            if (_locationPlugin.Enabled)
                await _locationPlugin.EventBatchProcessedAsync(startEventContexts).AnyContext();

            await SetSessionStartEventIdAsync(startContext.Project.Id, startContext.Event.GetSessionId(), startEvent.Id).AnyContext();
            return startEvent;
        }

        private async Task<string> UpdateSessionStartEventAsync(string projectId, string sessionId, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false) {
            string sessionStartEventId = await GetSessionStartEventIdAsync(projectId, sessionId).AnyContext();
            if (!String.IsNullOrEmpty(sessionStartEventId)) {
                await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, lastActivityUtc, isSessionEnd, hasError).AnyContext();

                if (isSessionEnd)
                    await _cache.RemoveAsync(GetSessionStartEventIdCacheKey(projectId, sessionId)).AnyContext();
            }

            return sessionStartEventId;
        }
    }
}