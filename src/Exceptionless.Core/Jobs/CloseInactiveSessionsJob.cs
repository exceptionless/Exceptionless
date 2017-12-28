using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Closes inactive user sessions.", InitialDelay = "30s", Interval = "30s")]
    public class CloseInactiveSessionsJob : JobWithLockBase {
        private readonly IEventRepository _eventRepository;
        private readonly ICacheClient _cache;
        private readonly ILockProvider _lockProvider;

        public CloseInactiveSessionsJob(IEventRepository eventRepository, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _eventRepository = eventRepository;
            _cache = cacheClient;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(CloseInactiveSessionsJob), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            var results = await _eventRepository.GetOpenSessionsAsync(SystemClock.UtcNow.SubtractMinutes(1), o => o.SnapshotPaging().PageLimit(100)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                var inactivePeriodUtc = SystemClock.UtcNow.Subtract(DefaultInactivePeriod);
                var sessionsToUpdate = new List<PersistentEvent>(results.Documents.Count);
                var cacheKeysToRemove = new List<string>(results.Documents.Count * 2);
                var existingSessionHeartbeatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var sessionStart in results.Documents) {
                    var lastActivityUtc = sessionStart.Date.UtcDateTime.AddSeconds((double)sessionStart.Value.GetValueOrDefault());
                    var heartbeatResult = await GetHeartbeatAsync(sessionStart).AnyContext();

                    bool closeDuplicate = heartbeatResult?.CacheKey != null && existingSessionHeartbeatIds.Contains(heartbeatResult.CacheKey);
                    if (heartbeatResult?.CacheKey != null && !closeDuplicate)
                        existingSessionHeartbeatIds.Add(heartbeatResult.CacheKey);

                    if (heartbeatResult != null && (closeDuplicate || heartbeatResult.Close || heartbeatResult.ActivityUtc > lastActivityUtc))
                        sessionStart.UpdateSessionStart(heartbeatResult.ActivityUtc, isSessionEnd: closeDuplicate || heartbeatResult.Close || heartbeatResult.ActivityUtc <= inactivePeriodUtc);
                    else if (lastActivityUtc <= inactivePeriodUtc)
                        sessionStart.UpdateSessionStart(lastActivityUtc, isSessionEnd: true);
                    else
                        continue;

                    sessionsToUpdate.Add(sessionStart);
                    if (heartbeatResult != null) {
                        cacheKeysToRemove.Add(heartbeatResult.CacheKey);
                        if (heartbeatResult.Close)
                            cacheKeysToRemove.Add(heartbeatResult.CacheKey + "-close");
                    }

                    Debug.Assert(sessionStart.Value != null && sessionStart.Value >= 0, "Session start value cannot be a negative number.");
                }

                if (sessionsToUpdate.Count > 0)
                    await _eventRepository.SaveAsync(sessionsToUpdate).AnyContext();

                if (cacheKeysToRemove.Count > 0)
                    await _cache.RemoveAllAsync(cacheKeysToRemove).AnyContext();

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }

            return JobResult.Success;
        }

        private async Task<HeartbeatResult> GetHeartbeatAsync(PersistentEvent sessionStart) {
            string sessionId = sessionStart.GetSessionId();
            if (!String.IsNullOrWhiteSpace(sessionId)) {
                var result = await GetLastHeartbeatActivityUtcAsync($"Project:{sessionStart.ProjectId}:heartbeat:{sessionId.ToSHA1()}").AnyContext();
                if (result != null)
                    return result;
            }

            var user = sessionStart.GetUserIdentity();
            if (String.IsNullOrWhiteSpace(user?.Identity))
                return null;

            return await GetLastHeartbeatActivityUtcAsync($"Project:{sessionStart.ProjectId}:heartbeat:{user.Identity.ToSHA1()}").AnyContext();
        }

        private async Task<HeartbeatResult> GetLastHeartbeatActivityUtcAsync(string cacheKey) {
            var cacheValue = await _cache.GetAsync<DateTime>(cacheKey).AnyContext();
            if (cacheValue.HasValue) {
                bool close = await _cache.GetAsync(cacheKey + "-close", false).AnyContext();
                return new HeartbeatResult { ActivityUtc =  cacheValue.Value, Close = close, CacheKey = cacheKey };
            }

            return null;
        }
        
        public TimeSpan DefaultInactivePeriod { get; set; } = TimeSpan.FromMinutes(5);
        
        private class HeartbeatResult {
            public DateTime ActivityUtc { get; set; }
            public string CacheKey { get; set; }
            public bool Close { get; set; }
        }
    }
}
