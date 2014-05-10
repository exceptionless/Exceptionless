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

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : MongoRepositoryOwnedByOrganization<Project>, IProjectRepository {
        private readonly OrganizationRepository _organizationRepository;

        public ProjectRepository(MongoDatabase database, OrganizationRepository organizationRepository, ICacheClient cacheClient = null) : base(database, cacheClient) {
            _organizationRepository = organizationRepository;
        }

        public Project GetByApiKey(string apiKey) {
            if (String.IsNullOrEmpty(apiKey))
                throw new ArgumentNullException("apiKey");

            return FindOne<Project>(new FindOptions().WithQuery(Query.EQ(FieldNames.ApiKeys, apiKey)).WithCacheKey(GetScopedCacheKey(apiKey)));
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
        
        public IEnumerable<TimeSpan> GetTargetTimeOffsetsForStats(string projectId) {
            return new[] { GetDefaultTimeOffset(projectId) };
        }

        public TimeSpan GetDefaultTimeOffset(string projectId) {
            return GetById(projectId, true).DefaultTimeZoneOffset();
        }

        public TimeZoneInfo GetDefaultTimeZone(string projectId) {
            return GetById(projectId, true).DefaultTimeZone();
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

        #region Collection Setup

        public const string CollectionName = "project";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public new static class FieldNames {
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

        protected override void AfterAdd(IList<Project> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            foreach (var project in documents)
                _organizationRepository.IncrementStats(project.OrganizationId, projectCount: 1);

            base.AfterAdd(documents, addToCache, expiresIn);
        }

        protected override void AfterRemove(IList<Project> documents, bool sendNotification = true) {
            foreach (var project in documents)
                _organizationRepository.IncrementStats(project.OrganizationId, projectCount: -1);

            base.AfterRemove(documents, sendNotification);
        }

        public override void InvalidateCache(Project entity) {
            foreach (string key in entity.ApiKeys)
                InvalidateCache(key);

            base.InvalidateCache(entity);
        }
        
        #endregion
    }
}