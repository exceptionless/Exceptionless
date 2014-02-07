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
using System.IO;

namespace CodeSmith.Core.IO
{
    public sealed class LockFile : LockBase<LockFile>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LockFile"/> class.
        /// </summary>
        /// <param name="fileName">The file.</param>
        public LockFile(string fileName) : base(fileName) {
            FileName = fileName;
        }

        public override string GetLockFilePath() {
            return FileName;
        }

        /// <summary>
        /// The file that is being locked.
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Checks to see if the specific path is locked.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Returns true if the lock has expired or the lock exists.</returns>
        public static bool IsLocked(string path) {
            if (String.IsNullOrEmpty(path))
                return false;

            return !IsLockExpired(path) && File.Exists(path);
        }
    }
}
