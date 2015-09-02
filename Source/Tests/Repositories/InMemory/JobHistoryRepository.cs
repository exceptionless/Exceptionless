#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class InMemoryJobHistoryRepository : InMemoryRepository<JobHistory>, IJobHistoryRepository {
        public InMemoryJobHistoryRepository(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {
        }

        public JobHistory GetMostRecent(string jobName) {
            return Collection.Where(d => d.Name == jobName).OrderByDescending(d => d.StartTime).FirstOrDefault();
        }
    }
}