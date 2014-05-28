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
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class InMemoryDayProjectStatsRepository : InMemoryRepositoryOwnedByProject<DayProjectStats>, IDayProjectStatsRepository {
        public InMemoryDayProjectStatsRepository(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }
        
        public ICollection<DayProjectStats> GetRange(string start, string end) {
            var query = Query.And(Query.GTE(FieldNames.Id, start), Query.LTE(FieldNames.Id, end));
            return Find<DayProjectStats>(new MultiOptions().WithQuery(query));
        }

        public long IncrementStats(string id, string stackId, long timeBucket, bool isNew) {
            UpdateBuilder update = Update
                .Inc(FieldNames.Total, 1)
                .Inc(FieldNames.NewTotal, isNew ? 1 : 0)
                .Inc(String.Format(FieldNames.MinuteStats_TotalFormat, timeBucket.ToString("0000")), 1)
                .Inc(String.Format(FieldNames.MinuteStats_NewTotalFormat, timeBucket.ToString("0000")), isNew ? 1 : 0)
                .Inc(String.Format(FieldNames.IdsFormat, stackId), 1)
                .Inc(String.Format(FieldNames.MinuteStats_IdsFormat, timeBucket.ToString("0000"), stackId), 1);

            if (isNew) {
                update.Push(FieldNames.NewStackIds, new BsonObjectId(new ObjectId(stackId)));
                update.Push(String.Format(FieldNames.MinuteStats_NewIdsFormat, timeBucket.ToString("0000")), new BsonObjectId(new ObjectId(stackId)));
            }

            return UpdateAll(new QueryOptions().WithId(id), update);
        }

        public void DecrementStatsByStackId(string projectId, string stackId) {
            var dayStats = GetByProjectId(projectId);
            foreach (DayProjectStats dayStat in dayStats) {
                if (!dayStat.StackIds.ContainsKey(stackId))
                    continue;

                int dayCount = dayStat.StackIds[stackId];
                UpdateBuilder update = Update.Inc(FieldNames.Total, -dayCount).Unset(String.Format(FieldNames.IdsFormat, stackId));

                if (dayStat.NewStackIds.Contains(stackId)) {
                    update.Inc(FieldNames.NewTotal, -1);
                    update.Pull(FieldNames.NewStackIds, new BsonObjectId(new ObjectId(stackId)));
                }

                foreach (var ms in dayStat.MinuteStats) {
                    if (!ms.Value.StackIds.ContainsKey(stackId))
                        continue;

                    int minuteCount = ms.Value.StackIds[stackId];

                    if (ms.Value.Total <= minuteCount) {
                        // Remove the entire node since total will be zero after removing our stats.
                        update.Unset(String.Format(FieldNames.MinuteStats_Format, ms.Key));
                    } else {
                        update.Inc(String.Format(FieldNames.MinuteStats_TotalFormat, ms.Key), -minuteCount);
                        update.Unset(String.Format(FieldNames.MinuteStats_IdsFormat, ms.Key, stackId));
                        if (ms.Value.NewStackIds.Contains(stackId)) {
                            update.Inc(String.Format(FieldNames.MinuteStats_NewTotalFormat, ms.Key), -1);
                            update.Pull(String.Format(FieldNames.MinuteStats_NewIdsFormat, ms.Key), stackId);
                        }
                    }
                }

                UpdateAll(new QueryOptions().WithId(dayStat.Id), update);
            }
        }

        #region Collection Setup

        public const string CollectionName = "project.stats.day";

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
            public const string MinuteStats = "mn";
            public const string MinuteStats_Format = "mn.{0}";
            public const string MinuteStats_Total = "tot";
            public const string MinuteStats_TotalFormat = "mn.{0}.tot";
            public const string MinuteStats_NewTotalFormat = "mn.{0}.new";
            public const string MinuteStats_IdsFormat = "mn.{0}.ids.{1}";
            public const string MinuteStats_NewIdsFormat = "mn.{0}.newids";
        }

        #endregion
    }
}