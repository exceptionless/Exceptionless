﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(80)]
    public class AutoSessionPlugin : EventProcessorPluginBase {
        private static readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(15);
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly UpdateStatsAction _updateStats;
        private readonly AssignToStackAction _assignToStack;
        private readonly LocationPlugin _locationPlugin;

        public AutoSessionPlugin(ICacheClient cacheClient, IEventRepository eventRepository, AssignToStackAction assignToStack, UpdateStatsAction updateStats, LocationPlugin locationPlugin) {
            _cacheClient = new ScopedCacheClient(cacheClient, "session");
            _eventRepository = eventRepository;
            _assignToStack = assignToStack;
            _updateStats = updateStats;
            _locationPlugin = locationPlugin;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var identityGroups = contexts.Where(c => c.Event.GetUserIdentity()?.Identity != null && String.IsNullOrEmpty(c.Event.GetSessionId()))
                .OrderBy(c => c.Event.Date)
                .GroupBy(c => c.Event.GetUserIdentity()?.Identity);
            
            foreach (var identityGroup in identityGroups) {
                string projectId = identityGroup.First().Project.Id;
                string cacheKey = $"{projectId}:identity:{identityGroup.Key.ToSHA1()}";
                string sessionId = await GetSessionIdAsync(cacheKey, projectId).AnyContext();
                EventContext sessionStartContext = null;

                var sessionsToUpdate = new Dictionary<string, EventContext>();
                foreach (var context in identityGroup) {
                    if (context.Event.IsSessionStart() || String.IsNullOrEmpty(sessionId)) {
                        if (context.Event.IsSessionStart() && !String.IsNullOrEmpty(sessionId)) {
                            await CreateSessionEndEventAsync(context, sessionId).AnyContext();
                            if (sessionStartContext != null)
                                sessionStartContext.Event.UpdateSessionStart(context.Event.Date.UtcDateTime, isSessionEnd: true);
                            else
                                await UpdateSessionStartEventAsync(context, sessionId, isSessionEnd: true);
                        }

                        sessionId = ObjectId.GenerateNewId(context.Event.Date.DateTime).ToString();
                        context.Event.SetSessionId(sessionId);
                        await _cacheClient.SetAsync(cacheKey, sessionId, _sessionTimeout).AnyContext();

                        if (!context.Event.IsSessionStart()) {
                            var lastContext = GetLastActivity(identityGroup.Where(c => c.Event.Date.Ticks > context.Event.Date.Ticks).ToList());
                            bool isSessionEnd = lastContext != null && (lastContext.Event.IsSessionStart() || lastContext.Event.IsSessionEnd());

                            sessionStartContext = await CreateSessionStartEventAsync(context, lastContext?.Event.Date.UtcDateTime, isSessionEnd).AnyContext();

                            if (lastContext == null || !isSessionEnd)
                                await _cacheClient.SetAsync(GetSessionStartEventIdCacheKey(context.Project.Id, sessionId), sessionStartContext.Event.Id, _sessionTimeout).AnyContext();
                        }
                    }

                    if (context.Event.IsSessionStart()) {
                        sessionStartContext = context;
                        continue;
                    }

                    context.Event.SetSessionId(sessionId);
                    if (sessionStartContext == null)
                        sessionsToUpdate[sessionId] = context;
                    else
                        sessionStartContext.Event.UpdateSessionStart(context.Event.Date.UtcDateTime, context.Event.IsSessionEnd());

                    if (context.Event.IsSessionEnd()) {
                        await _cacheClient.RemoveAllAsync(new [] {
                            cacheKey,
                            GetSessionStartEventIdCacheKey(context.Project.Id, sessionId)
                        }).AnyContext();

                        sessionId = null;
                    }
                }

                foreach (var pair in sessionsToUpdate)
                    await UpdateSessionStartEventAsync(pair.Value, pair.Key, pair.Value.Event.IsSessionEnd());
            }
        }

        private async Task<string> GetSessionIdAsync(string cacheKey, string projectId) {
            string sessionId = await _cacheClient.GetAsync<string>(cacheKey, null).AnyContext();
            if (!String.IsNullOrEmpty(sessionId)) {
                await _cacheClient.SetExpirationAsync(cacheKey, _sessionTimeout).AnyContext();
                await _cacheClient.SetExpirationAsync(GetSessionStartEventIdCacheKey(projectId, sessionId), _sessionTimeout).AnyContext();
            }

            return sessionId;
        }

        private string GetSessionStartEventIdCacheKey(string projectId, string sessionId) {
            return String.Concat(projectId, ":start:", sessionId);
        }

        private EventContext GetLastActivity(IList<EventContext> contexts) {
            EventContext lastContext = null;
            foreach (var context in contexts) {
                lastContext = context;

                if (context.Event.IsSessionEnd() || context.Event.IsSessionStart())
                    break;
            }

            return lastContext;
        }

        private async Task<EventContext> CreateSessionStartEventAsync(EventContext context, DateTime? lastActivityUtc, bool? isSessionEnd) {
            var startEvent = context.Event.ToSessionStartEvent(lastActivityUtc, isSessionEnd, context.Organization.HasPremiumFeatures);
            
            var startEventContexts = new List<EventContext> {
                new EventContext(startEvent) { Project = context.Project, Organization = context.Organization }
            };

            await _assignToStack.ProcessBatchAsync(startEventContexts).AnyContext();
            await _updateStats.ProcessBatchAsync(startEventContexts).AnyContext();
            await _eventRepository.AddAsync(startEvent).AnyContext();
            await _locationPlugin.EventBatchProcessedAsync(startEventContexts).AnyContext();

            return startEventContexts.Single();
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
        
        private async Task<string> UpdateSessionStartEventAsync(EventContext context, string sessionId, bool isSessionEnd = false, bool hasError = false) {
            string sessionStartEventIdCacheKey = $"{context.Project.Id}:start:{sessionId}";
            string sessionStartEventId = await _cacheClient.GetAsync<string>(sessionStartEventIdCacheKey, null).AnyContext();
            if (sessionStartEventId != null) {
                await _eventRepository.UpdateSessionStartLastActivityAsync(sessionStartEventId, context.Event.Date.UtcDateTime, isSessionEnd, hasError).AnyContext();

                if (isSessionEnd)
                    await _cacheClient.RemoveAsync(sessionStartEventIdCacheKey).AnyContext();
            }

            return sessionStartEventId;
        }
    }
}