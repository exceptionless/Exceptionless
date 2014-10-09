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
using Exceptionless.Models.Admin;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : MongoRepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(MongoDatabase database, IValidator<Token> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher)
        {
            _getIdValue = s => s;
        }

        public ICollection<Token> GetByTypeAndOrganizationId(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<Token>(new MongoOptions()
                .WithOrganizationId(organizationId)
                .WithQuery(Query.EQ(FieldNames.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public ICollection<Token> GetByTypeAndOrganizationIds(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new List<Token>();

            string cacheKey = String.Concat("type:", type, "-org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find<Token>(new MongoOptions()
                .WithOrganizationIds(organizationIds)
                .WithQuery(Query.EQ(FieldNames.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public ICollection<Token> GetByTypeAndProjectId(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var query = Query.And(Query.Or(
                            Query.EQ(FieldNames.ProjectId, new BsonObjectId(ObjectId.Parse(projectId))), 
                            Query.EQ(FieldNames.DefaultProjectId, new BsonObjectId(ObjectId.Parse(projectId)))
                        ), Query.EQ(FieldNames.Type, type));

            return Find<Token>(new MongoOptions()
                .WithQuery(query)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public ICollection<Token> GetByTypeAndOrganizationIdOrProjectId(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var query = Query.And(Query.Or(
                Query.EQ(FieldNames.OrganizationId, new BsonObjectId(ObjectId.Parse(organizationId))), 
                Query.EQ(FieldNames.ProjectId, new BsonObjectId(ObjectId.Parse(projectId))), 
                Query.EQ(FieldNames.DefaultProjectId, new BsonObjectId(ObjectId.Parse(projectId)))
            ), Query.EQ(FieldNames.Type, type));

            return Find<Token>(new MongoOptions()
                .WithQuery(query)
                .WithPaging(paging)
                .WithCacheKey(String.Concat("type:", type, "-org:", organizationId, "-project:", projectId))
                .WithExpiresIn(expiresIn));
        }

        public Token GetByRefreshToken(string refreshToken) {
            if (String.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException("refreshToken");

            return FindOne<Token>(new MongoOptions().WithQuery(Query.EQ(FieldNames.Refresh, refreshToken)));
        }

        #region Collection Setup

        public const string CollectionName = "token";

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string UserId = "uid";
            public const string ApplicationId = "aid";
            public const string DefaultProjectId = "def";
            public const string Refresh = "ref";
            public const string Type = "typ";
            public const string Scopes = "scp";
            public const string ExpiresUtc = "exp";
            public const string Notes = "not";
            public const string CreatedUtc = CommonFieldNames.Date;
            public const string ModifiedUtc = "mdt";
        }

        protected override string GetCollectionName() {
            return CollectionName;
        }

        protected override void ConfigureClassMap(BsonClassMap<Token> cm) {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.OrganizationId).SetElementName(FieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.UserId).SetElementName(FieldNames.UserId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.ApplicationId).SetElementName(FieldNames.ApplicationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.DefaultProjectId).SetElementName(FieldNames.DefaultProjectId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Refresh).SetElementName(FieldNames.Refresh).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type);
            cm.GetMemberMap(c => c.Scopes).SetElementName(FieldNames.Scopes).SetShouldSerializeMethod(obj => ((Token)obj).Scopes.Any());
            cm.GetMemberMap(c => c.ExpiresUtc).SetElementName(FieldNames.ExpiresUtc).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Notes).SetElementName(FieldNames.Notes).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.CreatedUtc).SetElementName(FieldNames.CreatedUtc);
            cm.GetMemberMap(c => c.ModifiedUtc).SetElementName(FieldNames.ModifiedUtc).SetIgnoreIfDefault(true);
        }

        #endregion

        public override void InvalidateCache(Token token) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("type:", token.Type, "-org:", token.OrganizationId)));
            Cache.Remove(GetScopedCacheKey(String.Concat("type:", token.Type, "-project:", token.ProjectId ?? token.DefaultProjectId)));
            Cache.Remove(GetScopedCacheKey(String.Concat("type:", token.Type, "-org:", token.OrganizationId, "-project:", token.ProjectId ?? token.DefaultProjectId)));
            base.InvalidateCache(token);
        }
    }
}