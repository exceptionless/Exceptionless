using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Provider;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A base class for job providers.
    /// </summary>
    public abstract class JobProvider : ProviderBase
    {
        /// <summary>
        /// Gets an <see cref="IEnumerable"/> list <see cref="IJobConfiguration"/> jobs.
        /// </summary>
        /// <returns>An <see cref="IEnumerable"/> list <see cref="IJobConfiguration"/> jobs.</returns>
        public abstract IEnumerable<IJobConfiguration> GetJobs();

        /// <summary>
        /// Determines whether a job reload is required.
        /// </summary>
        /// <param name="lastLoad">The last load.</param>
        /// <returns>
        /// 	<c>true</c> if a job reload is required; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// If true is return, the <see cref="JobManager"/> will call Reload that will 
        /// in turn call <see cref="GetJobs"/> on this provider.
        /// </remarks>
        public virtual bool IsReloadRequired(DateTime lastLoad)
        {
            return false;
        }
    }
}