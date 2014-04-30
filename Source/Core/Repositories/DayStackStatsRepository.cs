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
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class DayStackStatsRepository : MongoRepository<DayStackStats> {
        public DayStackStatsRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "stack.stats.day";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string MinuteStats_Format = "mn.{0}";
            public const string Id = "_id";
            public const string ProjectId = "pid";
            public const string ErrorStackId = "sid";
            public const string Total = "tot";
            public const string MinuteStats = "mn";
        }

        protected override string GetId(DayStackStats entity) {
            return entity.Id;
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ErrorStackId), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<DayStackStats> cm) {
            base.ConfigureClassMap(cm);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.StackId).SetElementName(FieldNames.ErrorStackId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.Total).SetElementName(FieldNames.Total);
            cm.GetMemberMap(c => c.MinuteStats).SetElementName(FieldNames.MinuteStats).SetSerializationOptions(DictionarySerializationOptions.Document);
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

        public void RemoveAllByErrorStackId(string errorStackId) {
            const int batchSize = 150;

            var ids = Collection.Find(Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId))))
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