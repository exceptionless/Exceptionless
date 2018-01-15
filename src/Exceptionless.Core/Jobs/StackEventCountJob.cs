using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Update event occurrence count for stacks.", InitialDelay = "2s", Interval = "5s")]
    public class StackEventCountJob : JobWithLockBase {
        private readonly StackService _stackService;
        private readonly ILockProvider _lockProvider;

        public StackEventCountJob(StackService stackService, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackService = stackService;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromSeconds(5));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(StackEventCountJob), TimeSpan.FromSeconds(5), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _logger.LogTrace("Start save stack event counts.");
            await _stackService.SaveStackUsagesAsync(cancellationToken: context.CancellationToken).AnyContext();
            _logger.LogTrace("Finished save stack event counts.");
            return JobResult.Success;
        }
    }
}
