using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
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
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.ToLowerInvariant().Trim();
            return FindOne<User>(new MongoOptions().WithQuery(Query.EQ(FieldNames.EmailAddress, emailAddress)).WithCacheKey(emailAddress));
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

        public static class FieldNames {
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
            foreach (var user in documents.Where(user => !String.IsNullOrWhiteSpace(user.EmailAddress)))
                user.EmailAddress = user.EmailAddress.ToLowerInvariant().Trim();

            base.BeforeAdd(documents);
        }

        protected override void BeforeSave(ICollection<User> originalDocuments, ICollection<User> documents) {
            foreach (var user in documents.Where(user => !String.IsNullOrWhiteSpace(user.EmailAddress)))
                user.EmailAddress = user.EmailAddress.ToLowerInvariant().Trim();

            base.BeforeSave(originalDocuments, documents);
        }

        protected override void AfterSave(ICollection<User> originalDocuments, ICollection<User> documents, bool addToCache = false, TimeSpan? expiresIn = null, bool sendNotifications = true) {
            if (EnableCache) {
                foreach (var document in documents) {
                    foreach (var organizationId in document.OrganizationIds) {
                        InvalidateCache(String.Concat("org:", organizationId));
                    }
                }
            }

            base.AfterSave(originalDocuments, documents, addToCache, expiresIn, sendNotifications);
        }

        public override void InvalidateCache(User user) {
            if (!EnableCache || Cache == null)
                return;

            InvalidateCache(user.EmailAddress.ToLowerInvariant());

            foreach (var organizationId in user.OrganizationIds)
                InvalidateCache(String.Concat("org:", organizationId));

            base.InvalidateCache(user);
        }
    }
}