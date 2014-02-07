using System;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A class representing the results from a job.
    /// </summary>
    [Serializable]
    public class JobResult : MarshalByRefObject
    {
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="JobResult"/> is cancelled.
        /// </summary>
        /// <value><c>true</c> if cancelled; otherwise, <c>false</c>.</value>
        public bool Cancelled { get; set; }

        /// <summary>
        /// Gets or sets the error for the job.
        /// </summary>
        /// <value>The error.</value>
        public Exception Error { get; set; }

        /// <summary>
        /// Gets or sets the result of the job.
        /// </summary>
        /// <value>The result of the job.</value>
        public object Result { get; set; }
    }
}