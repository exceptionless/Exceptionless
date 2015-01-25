using System;
using System.Threading;
using System.Threading.Tasks;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public abstract class JobBase {
        protected virtual IDisposable GetJobLock() {
            return null;
        }

        protected bool IsCancelPending(CancellationToken? token) {
            return token != null && token.Value.IsCancellationRequested;
        }

        public Task<JobResult> RunAsync(CancellationToken? token = null) {
            if (!token.HasValue)
                token = CancellationToken.None;

            try {
                using (GetJobLock())
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

        public async Task RunContinuousAsync(TimeSpan? delay = null, int iterationLimit = -1, CancellationToken? token = null) {
            if (!token.HasValue)
                token = CancellationToken.None;

            int iterations = 0;
            while (!IsCancelPending(token) && (iterationLimit < 0 || iterations < iterationLimit)) {
                Log.Trace().Message("Job \"{0}\" starting...", GetType().Name).Write();
                var result = await RunAsync(token);
                if (result != null) {
                    if (!result.IsSuccess)
                        Log.Error().Message("Job \"{0}\" failed: {1}", GetType().Name, result.Message).Exception(result.Error).Write();
                    else if (!String.IsNullOrEmpty(result.Message))
                        Log.Info().Message("Job \"{0}\" succeeded: {1}", GetType().Name, result.Message).Write();
                    else
                        Log.Trace().Message("Job \"{0}\" succeeded", GetType().Name).Write();
                } else {
                    Log.Error().Message("Null job result for \"{0}\".", GetType().Name).Write();
                }
                iterations++;
                if (delay.HasValue && delay.Value > TimeSpan.Zero)
                    await Task.Delay(delay.Value, token.Value);
            }
        }

        public void RunContinuous(TimeSpan? delay = null, int iterationLimit = -1, CancellationToken? token = null) {
            RunContinuousAsync(delay, iterationLimit, token).Wait();
        }
    }
}