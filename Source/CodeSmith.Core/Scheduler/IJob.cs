using System.Threading;
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
        /// <param name="token">The cancellation token used to cancel the job.</param>
        /// <returns>
        /// A <see cref="JobResult"/> instance indicating the results of the job.
        /// </returns>
        Task<JobResult> RunAsync(JobRunContext context, CancellationToken? token = null);
    }
}