using System;
using System.Collections.Generic;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A lock provider that only allows one job per <see cref="AppDomain"/>.
    /// </summary>
    public class StaticLockProvider : JobLockProvider
    {
        private static readonly HashSet<string> _locks = new HashSet<string>();
        private static readonly object _myLock = new object();

        /// <summary>
        /// Acquires a lock on specified job name.
        /// </summary>
        /// <param name="lockName">Name of the lock, usually the job name.</param>
        /// <returns>An <see cref="JobLock"/> object that will release the lock when disposed.</returns>
        public override JobLock Acquire(string lockName)
        {
            lock (_myLock)
            {
                bool lockAquired = _locks.Add(lockName);
                return new JobLock(this, lockName, lockAquired);
            }
        }

        /// <summary>
        /// Releases the specified job lock.
        /// </summary>
        /// <param name="jobLock">The job lock.</param>
        public override void Release(JobLock jobLock)
        {
            lock (_myLock)
            {
                if (jobLock.LockAcquired)
                    _locks.Remove(jobLock.LockName);

                jobLock.SetReleased();
            }
        }
    }
}