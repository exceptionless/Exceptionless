#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Messaging;
using FluentValidation;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class JobLockRepository : MongoRepository<JobLockInfo>, IJobLockRepository {
        public JobLockRepository(MongoDatabase database, IValidator<JobLockInfo> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        public JobLockInfo GetByName(string name) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");
            
            return FindOne<JobLockInfo>(new OneOptions().WithQuery(Query.EQ(FieldNames.Name, name)));
        }

        public void RemoveByAge(string name, TimeSpan age) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            RemoveAll(new QueryOptions().WithQuery(Query.And(Query.EQ(FieldNames.Name, name), Query.LT(FieldNames.CreatedDate, DateTime.Now.Subtract(age)))));
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

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string Name = "Name";
            public const string CreatedDate = "CreatedDate";
        }

        protected override string GetCollectionName() {
            return CollectionName;
        }

        #endregion
    }
}