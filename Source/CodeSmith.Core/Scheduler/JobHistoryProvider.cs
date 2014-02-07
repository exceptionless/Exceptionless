using System;
using System.Collections.Generic;
using System.Configuration.Provider;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A provider to save and restore the history for a job.
    /// </summary>
    public abstract class JobHistoryProvider : ProviderBase
    {
        /// <summary>
        /// Restores the latest job history from the provider.
        /// </summary>
        /// <param name="job">The job to restore the history to.</param>
        public abstract void RestoreHistory(Job job);

        /// <summary>
        /// Saves the history to the provider.
        /// </summary>
        /// <param name="job">The job to save the history on.</param>
        public abstract void SaveHistory(Job job);
    }
}
