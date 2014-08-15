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
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class InMemoryJobLockRepository : InMemoryRepository<JobLockInfo>, IJobLockRepository {
        public InMemoryJobLockRepository(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {
        }

        public JobLockInfo GetByName(string name) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");
            
            return Collection.FirstOrDefault(d => d.Name == name);
        }

        public void RemoveByAge(string name, TimeSpan age) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            Collection.RemoveAll(d => d.Name == name && d.CreatedDate < DateTime.Now.Subtract(age));
        }

        public void RemoveByName(string name) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            RemoveAll(new QueryOptions().WithQuery(Query.EQ(FieldNames.Name, name)));
        }

        public bool ExistsByName(string name) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            return Exists(new OneOptions().WithQuery(Query.EQ(FieldNames.Name, name)));
        }

        #region Collection Setup

        public const string CollectionName = "joblock";

        #endregion
    }
}