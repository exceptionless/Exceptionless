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
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Models;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : MongoRepository<User>, IUserRepository {
        public UserRepository(MongoDatabase database, IValidator<User> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, validator, cacheClient, messagePublisher) { }

        public User GetByEmailAddress(string emailAddress) {
            if (String.IsNullOrEmpty(emailAddress))
                return null;

            return FindOne<User>(new MongoOptions().WithQuery(Query.EQ(FieldNames.EmailAddress, emailAddress.ToLowerInvariant())).WithCacheKey(emailAddress));
        }

        public User GetByPasswordResetToken(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            return FindOne<User>(new MongoOptions().WithQuery(Query.EQ(FieldNames.PasswordResetToken, token)));
        }

        public User GetUserByOAuthProvider(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();
            return _collection.AsQueryable().FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider && o.ProviderUserId == providerUserId));
        }

        public User GetByVerifyEmailAddressToken(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            return FindOne<User>(new MongoOptions().WithQuery(Query.EQ(FieldNames.VerifyEmailAddressToken, token)));
        }

        // TODO: Have this return a limited subset of user data.
        public ICollection<User> GetByOrganizationId(string id) {
            if (String.IsNullOrEmpty(id))
                return new List<User>();

            var query = Query.In(FieldNames.OrganizationIds, new List<BsonValue> { new BsonObjectId(new ObjectId(id)) });
            return Find<User>(new MongoOptions().WithQuery(query).WithCacheKey(String.Concat("org:", id)));
        }

        #region Collection Setup

        public const string CollectionName = "user";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public new static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string EmailAddress = "EmailAddress";
            public const string IsEmailAddressVerified = "IsEmailAddressVerified";
            public const string EmailNotificationsEnabled = "EmailNotificationsEnabled";
            public const string VerifyEmailAddressToken = "VerifyEmailAddressToken";
            public const string OrganizationIds = "OrganizationIds";
            public const string OAuthAccounts_Provider = "OAuthAccounts.Provider";
            public const string OAuthAccounts_ProviderUserId = "OAuthAccounts.ProviderUserId";
            public const string PasswordResetToken = "PasswordResetToken";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys<User>.Ascending(u => u.OrganizationIds), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys<User>.Ascending(u => u.EmailAddress), IndexOptions.SetUnique(true).SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.OAuthAccounts_Provider, FieldNames.OAuthAccounts_ProviderUserId), IndexOptions.SetUnique(true).SetSparse(true).SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<User> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(p => p.OrganizationIds).SetSerializationOptions(new ArraySerializationOptions(new RepresentationSerializationOptions(BsonType.ObjectId)));
            cm.GetMemberMap(c => c.IsActive).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsEmailAddressVerified).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.Password).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.PasswordResetToken).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.PasswordResetTokenExpiration).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.Salt).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.VerifyEmailAddressToken).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.VerifyEmailAddressTokenExpiration).SetIgnoreIfDefault(true);
        }
        
        #endregion

        protected override void BeforeAdd(ICollection<User> documents) {
            foreach (var user in documents.Where(user => !String.IsNullOrEmpty(user.EmailAddress)))
                user.EmailAddress = user.EmailAddress.ToLowerInvariant();

            base.BeforeAdd(documents);
        }

        protected override void BeforeSave(ICollection<User> documents) {
            foreach (var user in documents.Where(user => !String.IsNullOrEmpty(user.EmailAddress)))
                user.EmailAddress = user.EmailAddress.ToLowerInvariant();

            base.BeforeSave(documents);
        }

        public override void InvalidateCache(User user) {
            if (Cache == null)
                return;

            //TODO: We should look into getting the original entity and reset the cache on the original email address as it might have changed.
            InvalidateCache(user.EmailAddress);

            foreach (var organizationId in user.OrganizationIds)
                InvalidateCache(String.Concat("org:", organizationId));
        }

        protected override void PublishMessage(EntityChangeType changeType, User user) {
            if (user.OrganizationIds.Any()) {
                foreach (var organizationId in user.OrganizationIds) {
                    var message = new EntityChanged {
                        ChangeType = changeType,
                        Id = user.Id,
                        OrganizationId = organizationId,
                        Type = _entityType
                    };

                    _messagePublisher.Publish(message);
                }
            } else {
                base.PublishMessage(changeType, user);
            }
        }
    }
}