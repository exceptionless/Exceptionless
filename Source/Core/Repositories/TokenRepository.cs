#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models.Admin;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : MongoRepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        public Token GetByRefreshToken(string refreshToken) {
            if (String.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException("refreshToken");

            return FindOne<Token>(new OneOptions().WithQuery(Query.EQ(FieldNames.Refresh, refreshToken)));
        }

        #region Collection Setup

        public const string CollectionName = "token";

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string UserId = "uid";
            public const string ApplicationId = "aid";
            public const string DefaultProjectId = "def";
            public const string Refresh = "ref";
            public const string Type = "typ";
            public const string Scopes = "scp";
            public const string ExpiresAt = "exp";
            public const string Notes = "not";
            public const string CreatedDate = CommonFieldNames.Date;
            public const string ModifiedDate = "mdt";
        }

        protected override string GetCollectionName() {
            return CollectionName;
        }

        protected override void ConfigureClassMap(BsonClassMap<Token> cm) {
            //base.ConfigureClassMap(cm);

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
            cm.GetMemberMap(c => c.ExpiresAt).SetElementName(FieldNames.ExpiresAt).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Notes).SetElementName(FieldNames.Notes).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.CreatedDate).SetElementName(FieldNames.CreatedDate);
            cm.GetMemberMap(c => c.ModifiedDate).SetElementName(FieldNames.ModifiedDate).SetIgnoreIfDefault(true);
        }

        #endregion
    }
}