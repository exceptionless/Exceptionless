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
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class InMemoryDayStackStatsRepository : InMemoryRepositoryOwnedByProjectAndStack<DayStackStats>, IDayStackStatsRepository {
        public InMemoryDayStackStatsRepository(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        public ICollection<DayStackStats> GetRange(string start, string end) {
            var query = Query.And(Query.GTE(FieldNames.Id, start), Query.LTE(FieldNames.Id, end));
            return Find<DayStackStats>(new MultiOptions().WithQuery(query));
        }

        public long IncrementStats(string id, long getTimeBucket) {
            UpdateBuilder update = Update
                .Inc(FieldNames.Total, 1)
                .Inc(String.Format(FieldNames.MinuteStats_Format, getTimeBucket.ToString("0000")), 1);

            return UpdateAll(new QueryOptions().WithId(id), update);
        }

        #region Collection Setup

        public const string CollectionName = "stack.stats.day";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string StackId = CommonFieldNames.StackId;
            public const string Total = "tot";
            public const string MinuteStats = "mn";
            public const string MinuteStats_Format = "mn.{0}";
        }

        #endregion\
    }
}