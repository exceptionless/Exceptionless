using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Extensions;
using Exceptionless.DateTimeExtensions;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// Job run completed event arguments.
    /// </summary>
    public class JobCompletedEventArgs : JobEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JobCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="action">The action.</param>
        /// <param name="started">The time the job run started.</param>
        /// <param name="finished">The time the job run ended.</param>
        /// <param name="result">The result of the job run.</param>
        /// <param name="status">The status of the job run.</param>
        public JobCompletedEventArgs(string jobName, JobAction action, DateTime started, DateTime finished, string result, JobStatus status)
            : this(jobName, action, String.Empty, started, finished, result, status)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JobCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="action">The action.</param>
        /// <param name="jobId">The job id.</param>
        /// <param name="started">The time the job run started.</param>
        /// <param name="finished">The time the job run ended.</param>
        /// <param name="result">The result of the job run.</param>
        /// <param name="status">The status of the job run.</param>
        public JobCompletedEventArgs(string jobName, JobAction action, string jobId, DateTime started, DateTime finished, string result, JobStatus status)
            : base(jobName, action, jobId)
        {
            Started = started;
            Finished = finished;
            Result = result;
            Status = status;
        }

        /// <summary>
        /// Gets or sets the time that the job run started.
        /// </summary>
        /// <value>The time the job run started.</value>
        public DateTime Started { get; private set; }

        /// <summary>
        /// Gets or sets the time that the job run finished.
        /// </summary>
        /// <value>The time the job run finished.</value>
        public DateTime Finished { get; private set; }

        /// <summary>
        /// Gets or sets the result of the job run.
        /// </summary>
        /// <value>The result of the job run.</value>
        public string Result { get; private set; }

        /// <summary>
        /// Gets or sets the status of the job run.
        /// </summary>
        /// <value>The status of the job run.</value>
        public JobStatus Status { get; private set; }

        /// <summary>
        /// Gets the duration of the job run.
        /// </summary>
        /// <value>The duration of the job run.</value>
        public TimeSpan Duration
        {
            get
            {
                return Finished.Subtract(Started);
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "Job '{0}' ({1}) {2} in {3}.".FormatWith(
                JobName,
                JobId,
                Action.ToString().ToLower(),
                Duration.ToWords(true)
            );
        }
    }
}
