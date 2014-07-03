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
    public class JobHistoryRepository : MongoRepository<JobHistory>, IJobHistoryRepository {
        public JobHistoryRepository(MongoDatabase database, IValidator<JobHistory> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, validator, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        public JobHistory GetMostRecent(string jobName) {
            return FindOne<JobHistory>(new OneOptions().WithQuery(Query.EQ(FieldNames.Name, jobName)).WithSort(SortBy.Descending(FieldNames.StartTime)));
        }

        #region Collection Setup

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string Name = "Name";
            public const string StartTime = "StartTime";
        }

        protected override void CreateCollection(MongoDatabase database) {
            CollectionOptionsBuilder options = CollectionOptions
                .SetCapped(true)
                .SetMaxSize(5 * 1024 * 1024);

            database.CreateCollection(GetCollectionName(), options);
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.Name, FieldNames.StartTime), IndexOptions.SetBackground(true));
        }

        public const string CollectionName = "jobhistory";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        #endregion
    }
}