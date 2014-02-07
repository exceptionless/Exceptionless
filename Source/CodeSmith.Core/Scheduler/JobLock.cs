using System;
using CodeSmith.Core.Component;

namespace CodeSmith.Core.Scheduler {
    /// <summary>
    /// A base class representing a job lock.
    /// </summary>
    public class JobLock : DisposableBase {
        /// <summary>
        /// Initializes a new instance of the <see cref="JobLock"/> class.
        /// </summary>
        /// <param name="provider">The lock provider to call when disposing.</param>
        /// <param name="lockName">Name of the lock.</param>
        /// <param name="lockAcquired">if set to <c>true</c> lock was acquired.</param>
        public JobLock(JobLockProvider provider, string lockName, bool lockAcquired) {
            _lockAcquired = lockAcquired;
            _lockName = lockName;
            _provider = provider;
        }

        private bool _lockAcquired;
        private readonly string _lockName;
        private readonly JobLockProvider _provider;

        /// <summary>
        /// Gets the name of the lock.
        /// </summary>
        /// <value>The name of the lock.</value>
        public string LockName { get { return _lockName; } }

        /// <summary>
        /// Gets a value indicating whether the lock was acquired successfully.
        /// </summary>
        /// <value><c>true</c> if the was lock acquired; otherwise, <c>false</c>.</value>
        public bool LockAcquired { get { return _lockAcquired; } }

        /// <summary>
        /// Disposes the unmanaged resources.
        /// </summary>
        protected override void DisposeUnmanagedResources() {
            if (!LockAcquired)
                return;

            _provider.Release(this);
        }

        /// <summary>
        /// Sets the lock to released.
        /// </summary>
        public void SetReleased() {
            _lockAcquired = false;
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString() {
            return String.Format("LockName: {0}, LockAcquired: {1}", LockName, LockAcquired);
        }
    }
}