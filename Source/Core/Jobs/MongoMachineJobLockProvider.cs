#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Jobs {
    public class MongoMachineJobLockProvider : MongoJobLockProvider {
        public MongoMachineJobLockProvider(IJobLockInfoRepository repository) : base(repository) {}

        public override JobLock Acquire(string lockName) {
            lockName = String.Format("{0} {1}", lockName, Environment.MachineName);
            return base.Acquire(lockName);
        }
    }
}