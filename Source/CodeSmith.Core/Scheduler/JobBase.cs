using System;
using System.ComponentModel;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A base class for jobs
    /// </summary>
    public abstract class JobBase : IJob
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
        public abstract JobResult Run(JobContext context);

        /// <summary>
        /// Cancels this job.
        /// </summary>
        public virtual void Cancel()
        {
            _cancelPending = true;
        }
    }
}