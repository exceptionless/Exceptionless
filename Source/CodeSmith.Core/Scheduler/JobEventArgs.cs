using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// Job event arguments.
    /// </summary>
    public class JobEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JobEventArgs"/> class.
        /// </summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="action">The action.</param>
        public JobEventArgs(string jobName, JobAction action)
            : this(jobName, action, String.Empty)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JobEventArgs"/> class.
        /// </summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="action">The action.</param>
        /// <param name="jobId">The job id.</param>
        public JobEventArgs(string jobName, JobAction action, string jobId)
        {
            JobName = jobName;
            Action = action;
            JobId = jobId;
        }

        /// <summary>
        /// Gets or sets the name of the job.
        /// </summary>
        /// <value>The name of the job.</value>
        public string JobName { get; private set; }

        /// <summary>
        /// Gets or sets the action.
        /// </summary>
        /// <value>The action.</value>
        public JobAction Action { get; private set; }

        /// <summary>
        /// Gets or sets the job id.
        /// </summary>
        /// <value>The job id.</value>
        public string JobId { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "Job '{0}' ({1}) {2}.".FormatWith(
                JobName,
                JobId,
                Action.ToString().ToLower());
        }
    }
}
