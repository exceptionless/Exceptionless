namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A default lock provider
    /// </summary>
    internal class DefaultJobLockProvider : JobLockProvider
    {
        /// <summary>
        /// Acquires a lock on specified job name.
        /// </summary>
        /// <param name="lockName">Name of the lock, usually the job name.</param>
        /// <returns>A <see cref="JobLock"/> object that will release the lock when disposed.</returns>
        public override JobLock Acquire(string lockName)
        {
            return new JobLock(this, lockName, true);
        }

        /// <summary>
        /// Releases the specified job lock.
        /// </summary>
        /// <param name="jobLock">The job lock.</param>
        public override void Release(JobLock jobLock)
        {
            //Do nothing for default
            jobLock.SetReleased();
        }
    }
}