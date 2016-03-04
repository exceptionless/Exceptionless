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
    public class CloseInactiveSessionsJob : JobBase {
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public CloseInactiveSessionsJob(IEventRepository eventRepository, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _eventRepository = eventRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(15));
        }

        protected override Task<ILock> GetJobLockAsync() {
            return _lockProvider.AcquireAsync(nameof(CloseInactiveSessionsJob), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobRunContext context) {
            const int LIMIT = 100;

            var results = await _eventRepository.GetOpenSessionsAsync(GetStartOfInactivePeriod(), new PagingOptions().WithPage(1).WithLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                var inactivePeriod = GetStartOfInactivePeriod();
                var sessionsToUpdate = new List<PersistentEvent>(LIMIT);

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
                
                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5)).AnyContext();

                await results.NextPageAsync().AnyContext();
                if (results.Documents.Count > 0)
                    await context.JobLock.RenewAsync().AnyContext();
            }

            return JobResult.Success;
        }

        private DateTime GetStartOfInactivePeriod() {
            return DateTime.UtcNow.Subtract(DefaultInactivePeriod);
        }
        
        public TimeSpan DefaultInactivePeriod { get; set; } = TimeSpan.FromMinutes(5);
    }
}
