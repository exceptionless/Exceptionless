using System;
using System.Threading.Tasks;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A base class for jobs
    /// </summary>
    public class Job : IJob
    {
        private volatile bool _cancelPending;

        /// <summary>
        /// Gets a value indicating whether a cancel request is pending.
        /// </summary>
        /// <value><c>true</c> if cancel is pending; otherwise, <c>false</c>.</value>
        protected bool CancelPending
        {
            get { return _cancelPending; }
        }

        /// <summary>
        /// Runs this job.
        /// </summary>
        /// <param name="context">The job context.</param>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public virtual Task<JobResult> RunAsync(JobRunContext context) {
            return Task.FromResult(JobResult.Success);
        }

        /// <summary>
        /// Runs this job.
        /// </summary>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public Task<JobResult> RunAsync() {
            return RunAsync(JobRunContext.Default);
        }

        /// <summary>
        /// Runs this job and waits for it to return.
        /// </summary>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public JobResult Run() {
            return RunAsync(JobRunContext.Default).Result;
        }

        /// <summary>
        /// Runs this job and waits for it to return.
        /// </summary>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public JobResult Run(JobRunContext context) {
            return RunAsync(context).Result;
        }

        /// <summary>
        /// Safely runs this job.
        /// </summary>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public JobResult TryRun() {
            try {
                return Run();
            } catch (Exception ex) {
                return new JobResult {
                    Error = ex,
                    Message = "Failed"
                };
            }
        }

        /// <summary>
        /// Safely runs this job.
        /// </summary>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        public JobResult TryRun(JobRunContext context) {
            try {
                return Run(context);
            } catch (Exception ex) {
                return new JobResult {
                    Error = ex,
                    Message = "Failed"
                };
            }
        }

        /// <summary>
        /// Cancels this job.
        /// </summary>
        public virtual void Cancel()
        {
            _cancelPending = true;
        }
    }
}