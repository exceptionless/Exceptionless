#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Jobs;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core {
    public class JobHistoryRepository : MongoRepositoryWithIdentity<JobHistory>, IJobHistoryRepository {
        public JobHistoryRepository(MongoDatabase database) : base(database) {}

        public new static class FieldNames {
            public const string Id = "_id";
            public const string Name = "Name";
            public const string StartTime = "StartTime";
        }

        protected override void CreateCollection(MongoDatabase database) {
            CollectionOptionsBuilder options = CollectionOptions
                .SetCapped(true)
                .SetMaxSize(5 * 1024 * 1024);

            database.CreateCollection(GetCollectionName(), options);
        }

        protected override void InitializeCollection(MongoCollection<JobHistory> collection) {
            base.InitializeCollection(collection);

            collection.CreateIndex(IndexKeys.Ascending(FieldNames.Name, FieldNames.StartTime));
        }

        public const string CollectionName = "jobhistory";

        protected override string GetCollectionName() {
            return CollectionName;
        }
    }
}