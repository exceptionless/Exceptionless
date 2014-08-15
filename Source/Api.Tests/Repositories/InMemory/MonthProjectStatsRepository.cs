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
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class MonthProjectStatsRepository : MongoRepositoryOwnedByProject<MonthProjectStats>, IMonthProjectStatsRepository {
        public MonthProjectStatsRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        public ICollection<MonthProjectStats> GetRange(string start, string end) {
            var query = Query.And(Query.GTE(FieldNames.Id, start), Query.LTE(FieldNames.Id, end));
            return Find<MonthProjectStats>(new MultiOptions().WithQuery(query));
        }
        
        public long IncrementStats(string id, string stackId, DateTime localDate, bool isNew) {
            UpdateBuilder update = Update
                .Inc(FieldNames.Total, 1)
                .Inc(FieldNames.NewTotal, isNew ? 1 : 0)
                .Inc(String.Format(FieldNames.DayStats_TotalFormat, localDate.Day), 1)
                .Inc(String.Format(FieldNames.DayStats_NewTotalFormat, localDate.Day), isNew ? 1 : 0)
                .Inc(String.Format(FieldNames.IdsFormat, stackId), 1)
                .Inc(String.Format(FieldNames.DayStats_IdsFormat, localDate.Day, stackId), 1);

            if (isNew) {
                update.Push(FieldNames.NewStackIds, new BsonObjectId(new ObjectId(stackId)));
                update.Push(String.Format(FieldNames.DayStats_NewIdsFormat, localDate.Day), new BsonObjectId(new ObjectId(stackId)));
            }

            return UpdateAll(new QueryOptions().WithId(id), update);
        }
        
        public void DecrementStatsByStackId(string projectId, string stackId) {
            var monthStats = GetByProjectId(projectId);
            foreach (var monthStat in monthStats) {
                if (!monthStat.StackIds.ContainsKey(stackId))
                    continue;

                int monthCount = monthStat.StackIds[stackId];

                UpdateBuilder update = Update.Inc(FieldNames.Total, -monthCount)
                    .Unset(String.Format(FieldNames.IdsFormat, stackId));

                if (monthStat.NewStackIds.Contains(stackId)) {
                    update.Inc(FieldNames.NewTotal, -1);
                    update.Pull(FieldNames.NewStackIds, new BsonObjectId(new ObjectId(stackId)));
                }

                foreach (var ds in monthStat.DayStats) {
                    if (!ds.Value.StackIds.ContainsKey(stackId))
                        continue;

                    int dayCount = ds.Value.StackIds[stackId];

                    if (ds.Value.Total <= dayCount) {
                        // remove the entire node since total will be zero after removing our stats
                        update.Unset(String.Format(FieldNames.DayStats_Format, ds.Key));
                    } else {
                        update.Inc(String.Format(FieldNames.DayStats_TotalFormat, ds.Key), -dayCount);
                        update.Unset(String.Format(FieldNames.DayStats_IdsFormat, ds.Key, stackId));
                        if (ds.Value.NewStackIds.Contains(stackId)) {
                            update.Inc(String.Format(FieldNames.DayStats_NewTotalFormat, ds.Key), -1);
                            update.Pull(String.Format(FieldNames.DayStats_NewIdsFormat, ds.Key), stackId);
                        }
                    }
                }

                UpdateAll(new QueryOptions().WithId(monthStat.Id), update);
            }
        }

        #region Collection Setup

        public const string CollectionName = "project.stats.month";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string IdsFormat = "ids.{0}";
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string Total = "tot";
            public const string NewTotal = "new";
            public const string NewStackIds = "newids";
            public const string DayStats = "day";
            public const string DayStats_Format = "day.{0}";
            public const string DayStats_Total = "tot";
            public const string DayStats_TotalFormat = "day.{0}.tot";
            public const string DayStats_NewTotalFormat = "day.{0}.new";
            public const string DayStats_IdsFormat = "day.{0}.ids.{1}";
            public const string DayStats_NewIdsFormat = "day.{0}.newids";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<MonthProjectStats> cm) {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.ProjectId).SetElementName(CommonFieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator());
            cm.GetMemberMap(c => c.DayStats).SetElementName(FieldNames.DayStats).SetSerializationOptions(DictionarySerializationOptions.Document);

            EventStatsHelper.MapStatsClasses();
        }

        #endregion
    }
}