#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.Caching;

namespace Exceptionless.Core.Lock {
    public class CacheLockProvider : ILockProvider {
        private readonly ICacheClient _cacheClient;

        public CacheLockProvider(ICacheClient cacheClient) {
            _cacheClient = cacheClient;
        }

        public IDisposable AcquireLock(string name, TimeSpan? lockTimeout = null, TimeSpan? acquireTimeout = null) {
            string cacheKey = GetCacheKey(name);

            Run.UntilTrue(() => {
                var lockValue = _cacheClient.Get<object>(cacheKey);
                if (lockValue != null)
                    return false;

                return _cacheClient.Add(cacheKey, DateTime.Now, lockTimeout ?? TimeSpan.FromMinutes(20));
            }, acquireTimeout);

            return new DisposableLock(name, this);
        }

        public void ReleaseLock(string name) {
            _cacheClient.Remove(GetCacheKey(name));
        }

        private string GetCacheKey(string name) {
            return String.Concat("lock:", name);
        }
    }
}