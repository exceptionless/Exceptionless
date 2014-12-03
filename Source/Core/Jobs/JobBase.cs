using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Lock;

namespace Exceptionless.Core.Jobs {
    public abstract class JobBase {
        protected ILockProvider LockProvider { get; set; }

        protected bool IsCancelPending(CancellationToken? token) {
            return token != null && token.Value.IsCancellationRequested;
        }

        public Task<JobResult> RunAsync(CancellationToken? token = null) {
            if (!token.HasValue)
                token = CancellationToken.None;

            if (LockProvider == null)
                return TryRunAsync(token.Value);

            try {
                using (LockProvider.AcquireLock(GetType().FullName, acquireTimeout: TimeSpan.FromMinutes(1)))
                    return TryRunAsync(token.Value);
            } catch (TimeoutException) {
                return Task.FromResult(JobResult.FailedWithMessage("Timeout attempting to acquire lock."));
            }
        }

        private Task<JobResult> TryRunAsync(CancellationToken token) {
            try {
                return RunInternalAsync(token);
            } catch (Exception ex) {
                return Task.FromResult(JobResult.FromException(ex));
            }
        }

        protected abstract Task<JobResult> RunInternalAsync(CancellationToken token);

        public JobResult Run(CancellationToken? token = null) {
            return RunAsync(token).Result;
        }

        public async Task RunContinuousAsync(int delay = 0, int iterationLimit = -1, CancellationToken? token = null) {
            if (!token.HasValue)
                token = CancellationToken.None;

            int iterations = 0;
            while (!IsCancelPending(token) && (iterationLimit < 0 || iterations < iterationLimit)) {
                var result = await RunAsync(token);
                iterations++;
                if (delay > 0)
                    await Task.Delay(delay, token.Value);
            }
        }

        public void RunContinuous(int delay = 0, int iterationLimit = -1, CancellationToken? token = null) {
            RunContinuousAsync(delay, iterationLimit, token).Wait();
        }
    }
}