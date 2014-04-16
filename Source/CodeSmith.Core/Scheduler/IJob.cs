using System.Threading.Tasks;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// Interface for a scheduled job.
    /// </summary>
    public interface IJob
    {
        /// <summary>
        /// Runs this job.
        /// </summary>
        /// <param name="context">The job context.</param>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        Task<JobResult> RunAsync(JobRunContext context);

        /// <summary>
        /// Cancels this job.
        /// </summary>
        void Cancel();
    }
}