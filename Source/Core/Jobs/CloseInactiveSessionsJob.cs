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
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Jobs {
    public class CloseInactiveSessionsJob : JobBase {
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public CloseInactiveSessionsJob(IEventRepository eventRepository, ICacheClient cacheClient) {
            _eventRepository = eventRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromHours(2));
        }

        protected override Task<ILock> GetJobLockAsync() {
            return _lockProvider.AcquireAsync(nameof(RetentionLimitsJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobRunContext context) {
            var results = await _eventRepository.GetOpenSessionsAsync(GetStartOfInactivePeriod(), new PagingOptions().WithPage(1).WithLimit(50)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                var inactivePeriod = GetStartOfInactivePeriod();
                var sessionsToUpdate = new List<PersistentEvent>(50);

                foreach (var sessionStart in results.Documents) {
                    var lastActivityUtc = sessionStart.Date.UtcDateTime.AddSeconds((double)sessionStart.Value.GetValueOrDefault());
                    if (lastActivityUtc > inactivePeriod)
                        continue;

                    sessionStart.UpdateSessionStart(lastActivityUtc, true);
                    sessionsToUpdate.Add(sessionStart);
                    
                    Debug.Assert(sessionStart.Value != null && sessionStart.Value >= 0, "Session start value cannot be a negative number.");
                }

                if (sessionsToUpdate.Count > 0)
                    await _eventRepository.SaveAsync(sessionsToUpdate).AnyContext();

                await results.NextPageAsync().AnyContext();
                if (results.Documents.Count > 0)
                    await context.JobLock.RenewAsync().AnyContext();
            }

            return JobResult.Success;
        }

        private static DateTime GetStartOfInactivePeriod() {
            return DateTime.UtcNow.SubtractMinutes(30);
        }
    }
}
