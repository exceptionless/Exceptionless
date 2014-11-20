using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A base class for jobs
    /// </summary>
    public abstract class Job : IJob
    {
        /// <summary>
        /// Gets the job lock provider to use when attempting to run the job.
        /// </summary>
        /// <value>The job lock provider.</value>
        protected JobLockProvider LockProvider { get; private set; }

        /// <summary>
        /// Gets the job run context used while running the job.
        /// </summary>
        /// <value>The job run context.</value>
        protected JobRunContext Context { get; private set; }

        /// <summary>
        /// Gets the cancellation token used to cancel the job.
        /// </summary>
        /// <value>The cancellation token.</value>
        protected CancellationToken CancellationToken { get; private set; }
        
        /// <summary>
        /// Gets a value indicating whether a cancel request is pending.
        /// </summary>
        /// <value><c>true</c> if cancel is pending; otherwise, <c>false</c>.</value>
        protected bool CancelPending { get { return CancellationToken.IsCancellationRequested; } }

        /// <summary>
        /// Runs this job.
        /// </summary>
        /// <param name="context">The job context.</param>
        /// <param name="token">The cancellation token used to cancel the job from running.</param>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public Task<JobResult> RunAsync(JobRunContext context = null, CancellationToken? token = null) {
            Context = context ?? JobRunContext.Default;
            CancellationToken = token ?? CancellationToken.None;

            return RunInternalAsync();
        }

        /// <summary>
        /// Implemented in each job to do the actual work.
        /// </summary>
        /// <returns>The job result.</returns>
        protected abstract Task<JobResult> RunInternalAsync();

        /// <summary>
        /// Runs this job and waits for it to return.
        /// </summary>
        /// <param name="context">The job context.</param>
        /// <param name="token">The cancellation token used to cancel the job from running.</param>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public JobResult Run(JobRunContext context = null, CancellationToken? token = null) {
            return RunAsync(context, token).Result;
        }

        /// <summary>
        /// Runs this job in a continuous loop.
        /// </summary>
        /// <param name="delay">Amount of time to delay between job runs.</param>
        /// <param name="iterationLimit">Maximum number of job run iterations.</param>
        /// <param name="context">The job context.</param>
        /// <param name="token">The cancellation token used to cancel the job from running.</param>
        public async Task RunContinuousAsync(int delay = 0, int iterationLimit = -1, JobRunContext context = null, CancellationToken? token = null) {
            if (iterationLimit <= 0)
                iterationLimit = Int32.MaxValue;

            int iterations = 0;
            while (!CancelPending && iterations < iterationLimit) {
                await RunAsync(context, token);
                iterations++;
                if (delay > 0)
                    Thread.Sleep(delay);
            }
        }

        /// <summary>
        /// Safely runs this job.
        /// </summary>
        /// <param name="context">The job context.</param>
        /// <param name="token">The cancellation token used to cancel the job from running.</param>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public JobResult TryRun(JobRunContext context = null, CancellationToken? token = null) {
            try {
                return Run(context, token);
            } catch (Exception ex) {
                return new JobResult {
                    Error = ex,
                    Message = "Failed"
                };
            }
        }
    }
}