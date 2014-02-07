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
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core.Repositories {
    public class MonthStackStatsRepository : MongoRepository<MonthStackStats> {
        public MonthStackStatsRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "errorstack.stats.month";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string DayStats_Format = "day.{0}";
            public const string Id = "_id";
            public const string ProjectId = "pid";
            public const string ErrorStackId = "sid";
            public const string Total = "tot";
            public const string DayStats = "day";
        }

        protected override string GetId(MonthStackStats entity) {
            return entity.Id;
        }

        protected override void InitializeCollection(MongoCollection<MonthStackStats> collection) {
            base.InitializeCollection(collection);

            collection.EnsureIndex(IndexKeys.Ascending(FieldNames.ProjectId));
            collection.EnsureIndex(IndexKeys.Ascending(FieldNames.ErrorStackId));
        }

        protected override void ConfigureClassMap(BsonClassMap<MonthStackStats> cm) {
            base.ConfigureClassMap(cm);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.Total).SetElementName(FieldNames.Total);
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.ErrorStackId).SetElementName(FieldNames.ErrorStackId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.DayStats).SetElementName(FieldNames.DayStats).SetSerializationOptions(DictionarySerializationOptions.Document);
        }

        public void RemoveAllByProjectId(string projectId) {
            const int batchSize = 150;

            BsonString[] ids = Collection
                .Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id)
                .Select(es => new BsonString(es.Id))
                .ToArray();

            while (ids.Length > 0) {
                Collection.Remove(Query.In(FieldNames.Id, ids));
                ids = Collection
                    .Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id)
                    .Select(es => new BsonString(es.Id))
                    .ToArray();
            }
        }

        public async Task RemoveAllByProjectIdAsync(string projectId) {
            await Task.Run(() => RemoveAllByProjectId(projectId));
        }

        public void RemoveAllByErrorStackId(string errorStackId) {
            const int batchSize = 150;

            BsonString[] ids = Collection.Find(Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId))))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id)
                .Select(es => new BsonString(es.Id))
                .ToArray();

            while (ids.Length > 0) {
                Collection.Remove(Query.In(FieldNames.Id, ids));
                ids = Collection.Find(Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId))))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id)
                    .Select(es => new BsonString(es.Id))
                    .ToArray();
            }
        }

        public async Task RemoveAllByErrorStackIdAsync(string errorStackId) {
            await Task.Run(() => RemoveAllByErrorStackId(errorStackId));
        }
    }
}