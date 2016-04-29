using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Jobs {
    public class CloseInactiveSessionsJob : JobWithLockBase {
        private readonly IEventRepository _eventRepository;
        private readonly ICacheClient _cacheClient;
        private readonly ILockProvider _lockProvider;

        public CloseInactiveSessionsJob(IEventRepository eventRepository, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _eventRepository = eventRepository;
            _cacheClient = cacheClient;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(15));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            return _lockProvider.AcquireAsync(nameof(CloseInactiveSessionsJob), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            const int LIMIT = 100;

            var results = await _eventRepository.GetOpenSessionsAsync(GetStartOfInactivePeriod(), new PagingOptions().WithPage(1).WithLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                var inactivePeriod = GetStartOfInactivePeriod();
                var sessionsToUpdate = new List<PersistentEvent>(LIMIT);
                var cacheKeysToRemove = new List<string>(LIMIT * 2);

                foreach (var sessionStart in results.Documents) {
                    var lastActivityUtc = sessionStart.Date.UtcDateTime.AddSeconds((double)sessionStart.Value.GetValueOrDefault());
                    var heartbeatResult = await GetHeartbeatAsync(sessionStart).AnyContext();

                    if (heartbeatResult != null && (heartbeatResult.Close || heartbeatResult.ActivityUtc > lastActivityUtc))
                        sessionStart.UpdateSessionStart(heartbeatResult.ActivityUtc, isSessionEnd: heartbeatResult.Close || heartbeatResult.ActivityUtc <= inactivePeriod);
                    else if (lastActivityUtc <= inactivePeriod)
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
                    await _cacheClient.RemoveAllAsync(cacheKeysToRemove).AnyContext();

                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5)).AnyContext();

                await results.NextPageAsync().AnyContext();
                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }

            return JobResult.Success;
        }

        private async Task<HeartbeatResult> GetHeartbeatAsync(PersistentEvent sessionStart) {
            var result = await GetLastHeartbeatActivityUtcAsync($"project:{sessionStart.ProjectId}:heartbeat:{sessionStart.GetSessionId().ToSHA1()}");
            if (result != null)
                return result;

            var user = sessionStart.GetUserIdentity();
            if (user == null)
                return null;

            return await GetLastHeartbeatActivityUtcAsync($"project:{sessionStart.ProjectId}:heartbeat:{user.Identity.ToSHA1()}");
        }

        private async Task<HeartbeatResult> GetLastHeartbeatActivityUtcAsync(string cacheKey) {
            var cacheValue = await _cacheClient.GetAsync<DateTime>(cacheKey).AnyContext();
            if (cacheValue.HasValue) {
                var close = await _cacheClient.GetAsync(cacheKey + "-close", false).AnyContext();
                return new HeartbeatResult { ActivityUtc =  cacheValue.Value, Close = close, CacheKey = cacheKey };
            }

            return null;
        }

        private DateTime GetStartOfInactivePeriod() {
            return DateTime.UtcNow.Subtract(DefaultInactivePeriod);
        }
        
        public TimeSpan DefaultInactivePeriod { get; set; } = TimeSpan.FromMinutes(5);
        
        private class HeartbeatResult {
            public DateTime ActivityUtc { get; set; }
            public string CacheKey { get; set; }
            public bool Close { get; set; }
        }
    }
}
