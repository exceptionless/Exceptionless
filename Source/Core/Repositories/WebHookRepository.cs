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
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class WebHookRepository : MongoRepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(MongoDatabase database, IValidator<WebHook> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, validator, cacheClient, messagePublisher) { }

        public void RemoveByUrl(string targetUrl) {
            RemoveAll(new MongoOptions().WithQuery(Query.EQ(FieldNames.Url, targetUrl)));
        }

        public ICollection<WebHook> GetByOrganizationIdOrProjectId(string organizationId, string projectId) {
            var query = Query.Or(
                    Query.EQ(FieldNames.OrganizationId, new BsonObjectId(ObjectId.Parse(organizationId))), 
                    Query.EQ(FieldNames.ProjectId, new BsonObjectId(ObjectId.Parse(projectId)))
                );

            return Find<WebHook>(new MongoOptions()
                .WithQuery(query)
                .WithCacheKey(String.Concat("org:", organizationId, "-project:", projectId))
                .WithExpiresIn(TimeSpan.FromMinutes(5)));
        }

        #region Collection Setup

        public const string CollectionName = "webhook";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string Url = "Url";
            public const string EventTypes = "EventTypes";
        }

        public static class EventTypes {
            // TODO: Add support for these new web hook types.
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
            cm.GetMemberMap(c => c.ProjectId).SetElementName(CommonFieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator()).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Url).SetElementName(FieldNames.Url);
            cm.GetMemberMap(c => c.EventTypes).SetElementName(FieldNames.EventTypes);
        }

        #endregion

        public override void InvalidateCache(WebHook hook) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("org:", hook.OrganizationId, "-project:", hook.ProjectId)));
            base.InvalidateCache(hook);
        }

    }
}