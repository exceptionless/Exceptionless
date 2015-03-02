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
using Exceptionless.Models.Admin;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class WebHookRepository : MongoRepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, cacheClient, messagePublisher) { }

        public void RemoveByUrl(string targetUrl) {
            RemoveAll(new QueryOptions().WithQuery(Query.EQ(FieldNames.Url, targetUrl)));
        }

        public ICollection<WebHook> GetByOrganizationIdOrProjectId(string organizationId, string projectId) {
            return Find<WebHook>(new MultiOptions()
                .WithOrganizationId(organizationId)
                .WithQuery(Query.Or(Query.NotExists(FieldNames.ProjectId), Query.EQ(FieldNames.ProjectId, projectId)))
                .WithCacheKey(String.Concat("org:", organizationId, "-project:", projectId))
                .WithExpiresIn(TimeSpan.FromMinutes(5)));
        }

        void IReadOnlyRepository<WebHook>.InvalidateCache(WebHook document) {
            InvalidateCache(document);
        }

        #region Collection Setup

        public const string CollectionName = "webhook";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string Url = "Url";
            public const string EventTypes = "EventTypes";
        }

        public static class EventTypes {
            public const string NewEvent = "NewEvent";
            public const string CriticalEvent = "CriticalEvent";
            public const string NewError = "NewError";
            public const string ErrorRegression = "ErrorRegression";
            public const string CriticalError = "CriticalError";
            public const string StackPromoted = "StackPromoted";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.Url), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<WebHook> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.Url).SetElementName(FieldNames.Url);
            cm.GetMemberMap(c => c.EventTypes).SetElementName(FieldNames.EventTypes);
        }
        public override void InvalidateCache(WebHook entity) {
            Cache.Remove(GetScopedCacheKey(entity.ProjectId));
            base.InvalidateCache(entity);
        }

        #endregion
    }
}