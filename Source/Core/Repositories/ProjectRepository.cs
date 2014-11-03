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
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class ProjectRepository : MongoRepositoryOwnedByOrganization<Project>, IProjectRepository {
        public ProjectRepository(MongoDatabase database, IValidator<Project> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {
        }

        public long GetCountByOrganizationId(string organizationId) {
            return _collection.Count(new OneOptions().WithOrganizationId(organizationId).GetMongoQuery(_getIdValue));
        }

        public void IncrementEventCounter(string projectId, long eventCount = 1) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException("projectId");

            if (eventCount < 1)
                return;

            var update = new UpdateBuilder();
            update.Inc(FieldNames.TotalEventCount, eventCount);
            update.Set(FieldNames.LastEventDate, new BsonDateTime(DateTime.UtcNow));

            UpdateAll(new QueryOptions().WithId(projectId), update);
            InvalidateCache(projectId);
        }
        
        public ICollection<Project> GetByNextSummaryNotificationOffset(byte hourToSendNotificationsAfterUtcMidnight, int limit = 10) {
            IMongoQuery query = Query.LT(FieldNames.NextSummaryEndOfDayTicks, new BsonInt64(DateTime.UtcNow.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
            return Find<Project>(new MongoOptions().WithQuery(query).WithFields(FieldNames.Id, FieldNames.NextSummaryEndOfDayTicks).WithLimit(limit));
        }

        public long IncrementNextSummaryEndOfDayTicks(ICollection<string> ids) {
            if (ids == null || !ids.Any())
                throw new ArgumentNullException("ids");

            UpdateBuilder update = Update.Inc(FieldNames.NextSummaryEndOfDayTicks, TimeSpan.TicksPerDay);
            return UpdateAll(new QueryOptions().WithIds(ids), update);
        }

        #region Collection Setup

        public const string CollectionName = "project";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string Name = "Name";
            public const string Configuration = "Configuration";
            public const string Configuration_Version = "Configuration.Version";
            public const string NotificationSettings = "NotificationSettings";
            public const string PromotedTabs = "PromotedTabs";
            public const string CustomContent = "CustomContent";
            public const string TotalEventCount = "TotalEventCount";
            public const string LastEventDate = "LastEventDate";
            public const string NextSummaryEndOfDayTicks = "NextSummaryEndOfDayTicks";
        }
       
        protected override void ConfigureClassMap(BsonClassMap<Project> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name);
            cm.GetMemberMap(c => c.Configuration).SetElementName(FieldNames.Configuration);
            cm.GetMemberMap(c => c.CustomContent).SetElementName(FieldNames.CustomContent).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.TotalEventCount).SetElementName(FieldNames.TotalEventCount);
            cm.GetMemberMap(c => c.LastEventDate).SetElementName(FieldNames.LastEventDate).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.NextSummaryEndOfDayTicks).SetElementName(FieldNames.NextSummaryEndOfDayTicks);

            cm.GetMemberMap(c => c.PromotedTabs).SetElementName(FieldNames.PromotedTabs).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Project)obj).PromotedTabs.Any());
            cm.GetMemberMap(c => c.NotificationSettings).SetElementName(FieldNames.NotificationSettings).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Project)obj).NotificationSettings.Any());
        }

        #endregion
    }
}