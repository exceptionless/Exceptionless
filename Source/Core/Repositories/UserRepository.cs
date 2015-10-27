using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : Repository<User>, IUserRepository {
        public UserRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<User> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) { }

        public Task<User> GetByEmailAddressAsync(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.ToLowerInvariant().Trim();
            var filter = Filter<User>.Term(u => u.EmailAddress, emailAddress);
            return FindOneAsync(new ElasticSearchOptions<User>().WithFilter(filter).WithCacheKey(emailAddress));
        }

        public Task<User> GetByPasswordResetTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.PasswordResetToken, token);
            return FindOneAsync(new ElasticSearchOptions<User>().WithFilter(filter));
        }

        public async Task<User> GetUserByOAuthProviderAsync(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();

            var filter = Filter<User>.Term(OrganizationIndex.Fields.User.OAuthAccountProviderUserId, new List<string>() { providerUserId });
            var results = (await FindAsync(new ElasticSearchOptions<User>().WithFilter(filter)).AnyContext()).Documents;

            return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
        }

        public Task<User> GetByVerifyEmailAddressTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.VerifyEmailAddressToken, token);
            return FindOneAsync(new ElasticSearchOptions<User>().WithFilter(filter));
        }

        public virtual Task<FindResults<User>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }
        
        public virtual Task<FindResults<User>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<User> { Documents = new List<User>(), Total = 0 });

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            var filter = Filter<User>.Term(u => u.OrganizationIds, organizationIds);
            return FindAsync(new ElasticSearchOptions<User>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<long> CountByOrganizationIdAsync(string organizationId) {
            var filter = Filter<User>.Term(u => u.OrganizationIds, new[] { organizationId });
            var options = new ElasticSearchOptions<User>()
                .WithFilter(filter);

            return CountAsync(options);
        }

        protected override async Task InvalidateCacheAsync(ICollection<User> users, ICollection<User> originalUsers) {
            if (!EnableCache)
                return;

            if (users == null)
                throw new ArgumentNullException(nameof(users));

            var combinedUsers = new List<User>();
            combinedUsers.AddRange(users);
            if (originalUsers != null)
                combinedUsers.AddRange(originalUsers);

            foreach (var emailAddress in combinedUsers.Select(u => u.EmailAddress).Distinct())
                await InvalidateCacheAsync(emailAddress).AnyContext();

            foreach (var organizationId in combinedUsers.SelectMany(u => u.OrganizationIds).Distinct())
                await InvalidateCacheAsync("org:" + organizationId).AnyContext();

            await base.InvalidateCacheAsync(users, originalUsers).AnyContext();
        }
    }
}