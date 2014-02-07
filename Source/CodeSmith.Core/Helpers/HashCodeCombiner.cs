#region Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.

// ------------------------------------------------------------------------------
// 
//  Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.
//  
//  The terms of use for this software are contained in the file
//  named sourcelicense.txt, which can be found in the root of this distribution.
//  By using this software in any fashion, you are agreeing to be bound by the
//  terms of this license.
// 
//  You must not remove this notice, or any other, from this software.
// 
// ------------------------------------------------------------------------------

#endregion

using System;
using System.Globalization;
using System.IO;
using System.Runtime;

using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Helpers
{
    public class HashCodeCombiner
    {
        private long _combinedHash = 5381L;

        public void Add(string s)
        {
            if (!String.IsNullOrEmpty(s))
                Add(s.GetStableHashCode());
        }

        public void AddCaseInsensitiveString(string s)
        {
            if (!String.IsNullOrEmpty(s))
                Add(s.ToLowerInvariant().GetStableHashCode());
        }

        public void Add(int n)
        {
            _combinedHash = ((_combinedHash << 5) + _combinedHash) ^ n;
        }

        public void Add(long n)
        {
            Add(n.GetHashCode());
        }

        public void Add(object o)
        {
            if (o != null)
                Add(o.GetHashCode());
        }

        public long CombinedHash
        {
#if !PFX_LEGACY_3_5
            [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]      
#endif
            get { return _combinedHash; }
        }

        public int CombinedHash32
        {
            get { return _combinedHash.GetHashCode(); }
        }

        public string CombinedHashString
        {
            get { return _combinedHash.ToString("x", CultureInfo.InvariantCulture); }
        }

        public void Add(DateTime dt)
        {
            Add(dt.GetHashCode());
        }

        public void AddFile(string fileName)
        {
            AddCaseInsensitiveString(fileName);
            if (File.Exists(fileName))
            {
                var info = new FileInfo(fileName);
                Add(info.CreationTimeUtc);
                Add(info.LastWriteTimeUtc);
                Add(info.Length);
            }
            else if (Directory.Exists(fileName))
            {
                AddDirectory(fileName);
            }
        }

        public void AddDirectory(string directoryName)
        {
            var info = new DirectoryInfo(directoryName);
            if (!info.Exists)
                return;

            AddCaseInsensitiveString(directoryName);
            foreach (var dir in info.GetDirectories())
                AddDirectory(dir.FullName);

            foreach (var file in info.GetFiles())
                AddFile(file.FullName);

            Add(info.CreationTimeUtc);
            Add(info.LastWriteTimeUtc);
        }
    }
}