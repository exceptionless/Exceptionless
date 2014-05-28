#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class MonthStackStatsRepository : MongoRepositoryOwnedByProjectAndStack<MonthStackStats>, IMonthStackStatsRepository {
        public MonthStackStatsRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        public ICollection<MonthStackStats> GetRange(string start, string end) {
            var query = Query.And(Query.GTE(FieldNames.Id, start), Query.LTE(FieldNames.Id, end));
            return Find<MonthStackStats>(new MultiOptions().WithQuery(query));
        }

        public long IncrementStats(string id, DateTime localDate) {
            var update = Update.Inc(FieldNames.Total, 1)
                               .Inc(String.Format(FieldNames.DayStats_Format, localDate.Day), 1);

            return UpdateAll(new QueryOptions().WithId(id), update);
        }

        #region Collection Setup

        public const string CollectionName = "stack.stats.month";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string DayStats_Format = "day.{0}";
            public const string Id = CommonFieldNames.Id;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string StackId = CommonFieldNames.StackId;
            public const string Total = "tot";
            public const string DayStats = "day";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.StackId), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<MonthStackStats> cm) {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.ProjectId).SetElementName(CommonFieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator());
            cm.GetMemberMap(c => c.StackId).SetElementName(CommonFieldNames.StackId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator());
            cm.GetMemberMap(c => c.Total).SetElementName(FieldNames.Total);
            cm.GetMemberMap(c => c.DayStats).SetElementName(FieldNames.DayStats).SetSerializationOptions(DictionarySerializationOptions.Document);
        }

        #endregion
    }
}