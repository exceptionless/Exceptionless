namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// The status state for a job
    /// </summary>
    public enum JobStatus
    {
        /// <summary>The default job status.</summary>
        None,
        /// <summary>The job is stopped.</summary>
        Stopped,
        /// <summary>The job is running.</summary>
        Running,
        /// <summary>The job is waiting for the next run time.</summary>
        Waiting,
        /// <summary>The job result was in error.</summary>
        Error,
        /// <summary>The job result was completed.</summary>
        Completed,
        /// <summary>The job result was canceled.</summary>
        Canceled
    }
}