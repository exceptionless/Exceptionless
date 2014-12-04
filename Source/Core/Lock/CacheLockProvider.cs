#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Diagnostics;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.Caching;
using NLog.Fluent;

namespace Exceptionless.Core.Lock {
    public class CacheLockProvider : ILockProvider {
        private readonly ICacheClient _cacheClient;

        public CacheLockProvider(ICacheClient cacheClient) {
            _cacheClient = cacheClient;
        }

        public IDisposable AcquireLock(string name, TimeSpan? lockTimeout = null, TimeSpan? acquireTimeout = null) {
            Log.Trace().Message("AcquireLock: {0}", name).Write();
            if (!acquireTimeout.HasValue)
                acquireTimeout = TimeSpan.FromMinutes(1);
            string cacheKey = GetCacheKey(name);

            Run.UntilTrue(() => {
                Log.Trace().Message("Checking to see if lock exists: {0}", name).Write();
                var lockValue = _cacheClient.Get<object>(cacheKey);
                Log.Trace().Message("Lock: {0} Value: {1}", name, lockValue ?? "<null>").Write();
                if (lockValue != null)
                    return false;

                Log.Trace().Message("Lock doesn't exist: {0}", name).Write();
                return _cacheClient.Add(cacheKey, DateTime.Now, lockTimeout ?? TimeSpan.FromMinutes(20));
            }, acquireTimeout);

            Log.Trace().Message("Returning lock: {0}", name).Write();
            return new DisposableLock(name, this);
        }

        public void ReleaseLock(string name) {
            Log.Trace().Message("ReleaseLock: {0}", name).Write();
            _cacheClient.Remove(GetCacheKey(name));
        }

        private string GetCacheKey(string name) {
            return String.Concat("lock:", name);
        }
    }
}