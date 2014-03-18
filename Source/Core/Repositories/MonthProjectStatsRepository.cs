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
using System.Threading.Tasks;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core.Repositories {
    public class MonthProjectStatsRepository : MongoRepository<MonthProjectStats> {
        public MonthProjectStatsRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "project.stats.month";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = "_id";
            public const string IdsFormat = "ids.{0}";
            public const string ProjectId = "pid";
            public const string Total = "tot";
            public const string NewTotal = "new";
            public const string NewErrorStackIds = "newids";
            public const string DayStats = "day";
            public const string DayStats_Format = "day.{0}";
            public const string DayStats_Total = "tot";
            public const string DayStats_TotalFormat = "day.{0}.tot";
            public const string DayStats_NewTotalFormat = "day.{0}.new";
            public const string DayStats_IdsFormat = "day.{0}.ids.{1}";
            public const string DayStats_NewIdsFormat = "day.{0}.newids";
        }

        protected override string GetId(MonthProjectStats entity) {
            return entity.Id;
        }

        protected override void InitializeCollection(MongoCollection<MonthProjectStats> collection) {
            base.InitializeCollection(collection);

            collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<MonthProjectStats> cm) {
            base.ConfigureClassMap(cm);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.DayStats).SetElementName(FieldNames.DayStats).SetSerializationOptions(DictionarySerializationOptions.Document);

            ErrorStatsHelper.MapStatsClasses();
        }

        public void RemoveAllByProjectId(string projectId) {
            const int batchSize = 150;

            BsonString[] ids = Collection.Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id)
                .Select(es => new BsonString(es.Id))
                .ToArray();

            while (ids.Length > 0) {
                Collection.Remove(Query.In(FieldNames.Id, ids));
                ids = Collection.Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id)
                    .Select(es => new BsonString(es.Id))
                    .ToArray();
            }
        }

        public async Task RemoveAllByProjectIdAsync(string projectId) {
            await Task.Run(() => RemoveAllByProjectId(projectId));
        }
    }
}