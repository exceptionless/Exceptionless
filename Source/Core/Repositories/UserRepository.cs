using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

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

        public virtual FindResults<User> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIds(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public long CountByOrganizationId(string organizationId) {
            var filter = Filter<User>.Term(u => u.OrganizationIds, new[] { organizationId });
            var options = new ElasticSearchOptions<User>()
                .WithFilter(filter);

            return Count(options);
        }

        public virtual FindResults<User> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new FindResults<User> { Documents = new List<User>(), Total = 0 };

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            var filter = Filter<User>.Term(u => u.OrganizationIds, organizationIds);
            return Find(new ElasticSearchOptions<User>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        protected override void InvalidateCache(ICollection<User> users, ICollection<User> originalUsers)
        {
            if (!EnableCache)
                return;

            if (users == null)
                throw new ArgumentNullException("users");

            var combinedUsers = new List<User>();
            combinedUsers.AddRange(users);
            if (originalUsers != null)
                combinedUsers.AddRange(originalUsers);

            combinedUsers
                .Select(u => u.EmailAddress)
                .Distinct()
                .ForEach(email => InvalidateCache(email));

            combinedUsers
                .SelectMany(u => u.OrganizationIds)
                .Distinct()
                .ForEach(organizationId => InvalidateCache("org:" + organizationId));

            base.InvalidateCache(users, originalUsers);
        }
    }
}