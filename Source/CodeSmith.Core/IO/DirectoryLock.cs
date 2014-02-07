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
using System.Diagnostics;
using System.IO;

namespace CodeSmith.Core.IO
{
    public sealed class DirectoryLock : LockBase<DirectoryLock>
    {
        /// <summary>
        /// The name of the lock file.
        /// </summary>
        private const string LockFileName = "dir.lock";

        public DirectoryLock(string directory) : base(directory) {
            Directory = Path.GetFullPath(directory);

            if (String.IsNullOrEmpty(Directory))
                throw new ArgumentException(String.Format("Directory '{0}' does not exist", Directory), "directory");

            try {
                if(!System.IO.Directory.Exists(Directory))
                    System.IO.Directory.CreateDirectory(Directory);
            } catch (Exception ex) {
                throw new ArgumentException(String.Format("Directory '{0}' could not be created.", Directory), "directory", ex);
            }
        }

        public override string GetLockFilePath() {
            return Path.Combine(Directory, LockFileName);
        }

        public static int ForceReleaseLock(string directory, bool includeSubdirectories = false) {
            int locksDeleted = 0;

            Debug.WriteLine("Force Releasing lock on path '{0}'.", directory);
            
            var searchOptions = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string lockfile in System.IO.Directory.GetFiles(directory, LockFileName, searchOptions)) {
                File.Delete(lockfile);
                locksDeleted++;
            }

            return locksDeleted;
        }

        /// <summary>
        /// The directory that is being locked.
        /// </summary>
        /// <value>The directory.</value>
        public string Directory { get; private set; }

        /// <summary>
        /// Checks to see if the specific directory is locked.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns>Returns true if the lock has expired or the lock exists.</returns>
        public static bool IsLocked(string directory) {
            if (String.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
                return false;

            var key = Path.Combine(directory, LockFileName);
            return !IsLockExpired(key) && File.Exists(key);
        }
    }
}
