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
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class ProjectRepository : MongoRepositoryOwnedByOrganization<Project>, IProjectRepository {
        private readonly IOrganizationRepository _organizationRepository;

        public ProjectRepository(MongoDatabase database, IOrganizationRepository organizationRepository, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _organizationRepository = organizationRepository;
        }

        public Project GetByApiKey(string apiKey) {
            if (String.IsNullOrEmpty(apiKey))
                throw new ArgumentNullException("apiKey");

            return FindOne<Project>(new OneOptions().WithQuery(Query.EQ(FieldNames.ApiKeys, apiKey)).WithCacheKey(apiKey));
        }

        public void IncrementStats(string projectId, long? eventCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException("projectId");

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

            UpdateAll(new QueryOptions().WithId(projectId), update);
            InvalidateCache(projectId);
        }

        public void SetStats(string projectId, long? eventCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException("projectId");

            var update = new UpdateBuilder();
            if (eventCount.HasValue)
                update.Set(FieldNames.EventCount, eventCount.Value);
            if (stackCount.HasValue)
                update.Set(FieldNames.StackCount, stackCount.Value);

            UpdateAll(new QueryOptions().WithId(projectId), update);
            InvalidateCache(projectId);
        }
        
        public ICollection<TimeSpan> GetTargetTimeOffsetsForStats(string projectId) {
            return new[] { GetDefaultTimeOffset(projectId) };
        }

        public TimeSpan GetDefaultTimeOffset(string projectId) {
            return GetById(projectId, true).DefaultTimeZoneOffset();
        }

        public TimeZoneInfo GetDefaultTimeZone(string projectId) {
            return GetById(projectId, true).DefaultTimeZone();
        }

        public DateTime UtcToDefaultProjectLocalTime(string id, DateTime utcDateTime) {
            TimeSpan offset = GetDefaultTimeOffset(id);
            return utcDateTime.Add(offset);
        }

        public DateTimeOffset UtcToDefaultProjectLocalTime(string id, DateTimeOffset dateTimeOffset) {
            return TimeZoneInfo.ConvertTime(dateTimeOffset, GetDefaultTimeZone(id));
        }

        public DateTime DefaultProjectLocalTimeToUtc(string id, DateTime dateTime) {
            if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue)
                return dateTime;

            TimeSpan offset = GetDefaultTimeOffset(id);
            return new DateTimeOffset(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, offset).UtcDateTime;
        }

        public ICollection<Project> GetByNextSummaryNotificationOffset(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10) {
            IMongoQuery query = Query.LT(FieldNames.NextSummaryEndOfDayTicks, new BsonInt64(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return Find<Project>(new MultiOptions().WithQuery(query).WithFields(FieldNames.Id, FieldNames.NextSummaryEndOfDayTicks).WithLimit(limit));
        }

        public long IncrementNextSummaryEndOfDayTicks(ICollection<string> ids) {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException("ids");

            UpdateBuilder update = Update.Inc(FieldNames.NextSummaryEndOfDayTicks, TimeSpan.TicksPerDay);
            return UpdateAll(new QueryOptions().WithIds(ids), update);
        }

        protected override void AfterRemove(ICollection<Project> documents, bool sendNotification = true) {
            foreach (var project in documents)
                _organizationRepository.IncrementStats(project.OrganizationId, projectCount: -1);

            base.AfterRemove(documents, sendNotification);
        }

        protected override void AfterAdd(ICollection<Project> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            foreach (var project in documents)
                _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);

            base.AfterAdd(documents, addToCache, expiresIn);
        }

        public override void InvalidateCache(Project entity) {
            foreach (string key in entity.ApiKeys)
                InvalidateCache(key);

            base.InvalidateCache(entity);
        }

        #region Collection Setup

        public const string CollectionName = "project";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
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
        
        #endregion
    }
}