//------------------------------------------------------------------------------
//
// Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.
// 
// The terms of use for this software are contained in the file
// named sourcelicense.txt, which can be found in the root of this distribution.
// By using this software in any fashion, you are agreeing to be bound by the
// terms of this license.
// 
// You must not remove this notice, or any other, from this software.
//
//------------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Helpers;
using Exceptionless.DateTimeExtensions;

namespace CodeSmith.Core.IO
{
    public abstract class LockBase<T> : DisposableBase where T : LockBase<T> {
        private static double _defaultTimeOutInSeconds = 30;
        protected static ConcurrentDictionary<string, bool> _lockStatus = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Ensures that the derived classes always have a string parameter to pass in a path.
        /// </summary>
        /// <param name="path"></param>
        protected LockBase(string path) {}

        /// <summary>
        /// Acquires a lock while waiting with the default timeout value.
        /// </summary>
        public virtual void AcquireLock() {
            AcquireLock(TimeSpan.FromSeconds(DefaultTimeOutInSeconds));
        }

        /// <summary>
        /// Acquires a lock in a specific amount of time.
        /// </summary>
        /// <param name="timeout">The time to wait for when trying to acquire a lock.</param>
        public void AcquireLock(TimeSpan timeout) {
            CreateLock(GetLockFilePath(), timeout);
        }

        /// <summary>
        /// Acquires a lock while waiting with the default timeout value.
        /// </summary>
        /// <param name="path">The path to acquire a lock on.</param>
        /// <returns>A lock instance.</returns>
        public static T Acquire(string path) {
            return Acquire(path, TimeSpan.FromSeconds(DefaultTimeOutInSeconds));
        }

        /// <summary>
        /// Acquires a lock in a specific amount of time.
        /// </summary>
        /// <param name="path">The path to acquire a lock on.</param>
        /// <param name="timeout">The time to wait for when trying to acquire a lock.</param>
        /// <returns>A lock instance.</returns>
        public static T Acquire(string path, TimeSpan timeout) {
#if !SILVERLIGHT
            var lockInstance = Activator.CreateInstance(typeof(T), path) as T;
            if (lockInstance == null)
                throw new Exception("Unable to locking instance.");

            lockInstance.AcquireLock(timeout);
            return lockInstance;
#else
            throw new NotImplementedException();
#endif
        }

        /// <summary>
        /// Creates a lock file.
        /// </summary>
        /// <param name="path">The place to create the lock file.</param>
        /// <param name="timeout">The amount of time to wait before a TimeoutException is thrown.</param>
        protected virtual void CreateLock(string path, TimeSpan timeout) {
            DateTime expire = DateTime.UtcNow.Add(timeout);

            Retry:
            while (File.Exists(path)) {
                if (expire < DateTime.UtcNow) {
                    string message = String.Format("[Thread: {0}] The lock '{1}' timed out.", Thread.CurrentThread.ManagedThreadId, path);
                    
                    Debug.WriteLine(message);
                    throw new TimeoutException(message);
                }

                if (IsLockExpired(path)) {
                    Debug.WriteLine("[Thread: {0}] The lock '{1}' has expired on '{2}' and wasn't cleaned up properly.", Thread.CurrentThread.ManagedThreadId, path, File.GetCreationTimeUtc(path));
                    ReleaseLock();
                } else {
                    Debug.WriteLine("[Thread: {0}] Waiting for lock: {1}", Thread.CurrentThread.ManagedThreadId, path);
                    Thread.Sleep(500);
                }
            }

            // create file 
            try {
                using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    fs.Close();

                _lockStatus[path] = true;
            }
            catch (IOException) {
                Debug.WriteLine(String.Format("[Thread: {0}] Error creating lock: {1}", Thread.CurrentThread.ManagedThreadId, path));
                goto Retry;
            }

            Debug.WriteLine(String.Format("[Thread: {0}] Created lock: {1}", Thread.CurrentThread.ManagedThreadId, path));
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        public virtual void ReleaseLock() {
            RemoveLock(GetLockFilePath());
        }

        public abstract string GetLockFilePath();

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <param name="path">The path to the lock file.</param>
        protected virtual void RemoveLock(string path) {
            Run.WithRetries(() => {
                try {
                    if (!File.Exists(path))
                        return;

                    File.Delete(path);

                    _lockStatus[path] = false;

                    Debug.WriteLine("[Thread: {0}] Deleted lock: {1}", Thread.CurrentThread.ManagedThreadId, path);
                }
                catch (IOException) {
                    Debug.WriteLine("[Thread: {0}] Error creating lock: {1}", Thread.CurrentThread.ManagedThreadId, path);
                    throw;
                }
            }, 5);
        }

        /// <summary>
        /// The default time to wait when trying to acquire a lock.
        /// </summary>
        protected static double DefaultTimeOutInSeconds {
            get { return _defaultTimeOutInSeconds; } 
            set { _defaultTimeOutInSeconds = value; }
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        protected override void DisposeUnmanagedResources() {
            ReleaseLock();
        }

        protected static bool IsLockExpired(string path) {
            if(String.IsNullOrEmpty(path))
                throw new ArgumentException("path cannot be null.", "path");
            
            return (!_lockStatus.ContainsKey(path) || !_lockStatus[path])
                && File.GetCreationTimeUtc(path) < DateTime.UtcNow.SubtractSeconds(DefaultTimeOutInSeconds * 10);
        }
    }
}
