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
    public class DayProjectStatsRepository : MongoRepository<DayProjectStats> {
        public DayProjectStatsRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "project.stats.day";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = "_id";
            public const string IdsFormat = "ids.{0}";
            public const string ProjectId = "pid";
            public const string Total = "tot";
            public const string NewTotal = "new";
            public const string NewStackIds = "newids";
            public const string MinuteStats = "mn";
            public const string MinuteStats_Format = "mn.{0}";
            public const string MinuteStats_Total = "tot";
            public const string MinuteStats_TotalFormat = "mn.{0}.tot";
            public const string MinuteStats_NewTotalFormat = "mn.{0}.new";
            public const string MinuteStats_IdsFormat = "mn.{0}.ids.{1}";
            public const string MinuteStats_NewIdsFormat = "mn.{0}.newids";
        }

        protected override string GetId(DayProjectStats entity) {
            return entity.Id;
        }

        protected override void InitializeCollection(MongoCollection<DayProjectStats> collection) {
            base.InitializeCollection(collection);

            collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<DayProjectStats> cm) {
            base.ConfigureClassMap(cm);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.MinuteStats).SetElementName(FieldNames.MinuteStats).SetSerializationOptions(DictionarySerializationOptions.Document);

            EventStatsHelper.MapStatsClasses();
        }

        public void RemoveAllByProjectId(string projectId) {
            const int batchSize = 150;

            var ids = Collection.Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
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