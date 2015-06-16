using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;
using Nest;
using IndexOptions = MongoDB.Driver.Builders.IndexOptions;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : ElasticSearchRepository<User>, IUserRepository {
        public UserRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<User> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) { }

        public User GetByEmailAddress(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.ToLowerInvariant().Trim();
            var filter = Filter<User>.Term(u => u.EmailAddress, emailAddress);
            return FindOne(new ElasticSearchOptions<User>().WithFilter(filter).WithCacheKey(emailAddress));
        }

        public User GetByPasswordResetToken(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.PasswordResetToken, token);
            return FindOne(new ElasticSearchOptions<User>().WithFilter(filter));
        }

        public User GetUserByOAuthProvider(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();

            var filter = Filter<User>.Term(OrganizationIndex.Fields.User.OAuthAccountProviderUserId, new List<string>() { providerUserId });
            var results = Find(new ElasticSearchOptions<User>().WithFilter(filter)).Documents;

            return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
        }

        public User GetByVerifyEmailAddressToken(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.VerifyEmailAddressToken, token);
            return FindOne(new ElasticSearchOptions<User>().WithFilter(filter));
        }

        public FindResults<User> GetByOrganizationId(string id) {
            if (String.IsNullOrEmpty(id))
                return new FindResults<User>();

            var filter = Filter<User>.Term(u => u.OrganizationIds, new List<string>() { id });
            return Find(new ElasticSearchOptions<User>().WithFilter(filter).WithCacheKey(String.Concat("org:", id)));
        }

        public long CountByOrganizationId(string organizationId) {
            return Count(new ElasticSearchOptions<User>().WithOrganizationId(organizationId));
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

        //protected override void InitializeCollection(MongoDatabase database) {
        //    base.InitializeCollection(database);

        //    _collection.CreateIndex(IndexKeys<User>.Ascending(u => u.OrganizationIds), IndexOptions.SetBackground(true));
        //    _collection.CreateIndex(IndexKeys<User>.Ascending(u => u.EmailAddress), IndexOptions.SetUnique(true).SetBackground(true));
        //    _collection.CreateIndex(IndexKeys.Ascending(FieldNames.OAuthAccounts_Provider, FieldNames.OAuthAccounts_ProviderUserId), IndexOptions.SetUnique(true).SetSparse(true).SetBackground(true));
        //}

        //protected override void ConfigureClassMap(BsonClassMap<User> cm) {
        //    base.ConfigureClassMap(cm);
        //    cm.GetMemberMap(p => p.OrganizationIds).SetSerializationOptions(new ArraySerializationOptions(new RepresentationSerializationOptions(BsonType.ObjectId)));
        //    cm.GetMemberMap(c => c.IsActive).SetIgnoreIfDefault(true);
        //    cm.GetMemberMap(c => c.IsEmailAddressVerified).SetIgnoreIfDefault(true);
        //    cm.GetMemberMap(c => c.Password).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.PasswordResetToken).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.PasswordResetTokenExpiration).SetIgnoreIfDefault(true);
        //    cm.GetMemberMap(c => c.Salt).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.VerifyEmailAddressToken).SetIgnoreIfNull(true);
        //    cm.GetMemberMap(c => c.VerifyEmailAddressTokenExpiration).SetIgnoreIfDefault(true);
        //}
        
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