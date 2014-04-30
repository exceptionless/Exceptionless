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
using System.Linq;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core {
    public class ProjectRepository : MongoRepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "project";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public new static class FieldNames {
            public const string Id = "_id";
            public const string OrganizationId = "oid";
            public const string Name = "Name";
            public const string TimeZone = "TimeZone";
            public const string ApiKeys = "ApiKeys";
            public const string Configuration = "Configuration";
            public const string Configuration_Version = "Configuration.Version";
            public const string NotificationSettings = "NotificationSettings";
            public const string PromotedTabs = "PromotedTabs";
            public const string CustomContent = "CustomContent";
            public const string StackCount = "StackCount";
            public const string EventCount = "EventCount";
            public const string TotalEventCount = "TotalEventCount";
            public const string LastEventDate = "LastEventDate";
            public const string NextSummaryEndOfDayTicks = "NextSummaryEndOfDayTicks";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ApiKeys), IndexOptions.SetUnique(true).SetSparse(true));
            // TODO: Should we set an index on project and configuration key name.
        }

        protected override void ConfigureClassMap(BsonClassMap<Project> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(p => p.ApiKeys).SetShouldSerializeMethod(obj => ((Project)obj).ApiKeys.Any()); // Only serialize API keys if it is populated.
        }

        public override Project Add(Project entity, bool addToCache = false) {
            foreach (string key in entity.ApiKeys)
                InvalidateCache(key);

            return base.Add(entity, addToCache);
        }

        public override Project Update(Project entity, bool addToCache = false) {
            entity.Configuration.Version++;
            entity = base.Update(entity, addToCache);

            return GetById(entity.Id);
        }

        public override void InvalidateCache(Project entity) {
            foreach (string key in entity.ApiKeys)
                InvalidateCache(key);
            base.InvalidateCache(entity);
        }

        public Project GetByApiKey(string apiKey) {
            if (Cache == null)
                return _collection.FindOneAs<Project>(Query.EQ(FieldNames.ApiKeys, apiKey));

            var result = Cache.Get<Project>(GetScopedCacheKey(apiKey));
            if (result == null) {
                result = _collection.FindOneAs<Project>(Query.EQ(FieldNames.ApiKeys, apiKey));
                if (result != null)
                    Cache.Set(GetScopedCacheKey(apiKey), result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        public IEnumerable<TimeSpan> GetTargetTimeOffsetsForStats(string projectId) {
            return new[] { GetDefaultTimeOffset(projectId) };
        }

        public TimeSpan GetDefaultTimeOffset(string projectId) {
            return GetByIdCached(projectId).DefaultTimeZoneOffset();
        }

        public TimeZoneInfo GetDefaultTimeZone(string projectId) {
            return GetByIdCached(projectId).DefaultTimeZone();
        }

        public DateTime UtcToDefaultProjectLocalTime(string projectId, DateTime utcDateTime) {
            TimeSpan offset = GetDefaultTimeOffset(projectId);
            return utcDateTime.Add(offset);
        }

        public DateTimeOffset UtcToDefaultProjectLocalTime(string projectId, DateTimeOffset dateTimeOffset) {
            return TimeZoneInfo.ConvertTime(dateTimeOffset, GetDefaultTimeZone(projectId));
        }

        public DateTime DefaultProjectLocalTimeToUtc(string projectId, DateTime dateTime) {
            if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue)
                return dateTime;

            TimeSpan offset = GetDefaultTimeOffset(projectId);
            return new DateTimeOffset(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, offset).UtcDateTime;
        }

        public void IncrementStats(string projectId, long? eventCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException("projectId");

            IMongoQuery query = Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(projectId)));

            var update = new UpdateBuilder();
            if (eventCount.HasValue && eventCount.Value != 0) {
                update.Inc(FieldNames.EventCount, eventCount.Value);
                if (eventCount.Value > 0) {
                    update.Inc(FieldNames.TotalEventCount, eventCount.Value);
                    update.Set(FieldNames.LastEventDate, new BsonDateTime(DateTime.UtcNow));
                }
            }
            if (stackCount.HasValue && stackCount.Value != 0)
                update.Inc(FieldNames.StackCount, stackCount.Value);

            Collection.Update(query, update);
            InvalidateCache(projectId);
        }

        public void SetStats(string projectId, long? eventCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException("projectId");

            IMongoQuery query = Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(projectId)));

            var update = new UpdateBuilder();
            if (eventCount.HasValue)
                update.Set(FieldNames.EventCount, eventCount.Value);
            if (stackCount.HasValue)
                update.Set(FieldNames.StackCount, stackCount.Value);

            Collection.Update(query, update);
            InvalidateCache(projectId);
        }
    }
}