using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : RepositoryBase<User>, IUserRepository {
        public UserRepository(ExceptionlessElasticConfiguration configuration, IValidator<User> validator, ICacheClient cache, IMessagePublisher messagePublisher, ILogger<UserRepository> logger) 
            : base(configuration.Client, validator, cache, messagePublisher, logger) {
            ElasticType = configuration.Organizations.User;
        }

        public Task<User> GetByEmailAddressAsync(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.ToLowerInvariant().Trim();
            var filter = Filter<User>.Term(u => u.EmailAddress, emailAddress);
            return FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter).WithCacheKey(emailAddress));
        }

        public Task<User> GetByPasswordResetTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.PasswordResetToken, token);
            return FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public async Task<User> GetUserByOAuthProviderAsync(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();

            var filter = Filter<User>.Term(UserIndexType.Fields.OAuthAccountProviderUserId, new List<string>() { providerUserId });
            var results = (await FindAsync(new ExceptionlessQuery().WithElasticFilter(filter)).AnyContext()).Documents;

            return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
        }

        public Task<User> GetByVerifyEmailAddressTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.VerifyEmailAddressToken, token);
            return FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public virtual Task<IFindResults<User>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public virtual Task<IFindResults<User>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult<IFindResults<User>>(new FindResults<User>());

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            var filter = Filter<User>.Term(u => u.OrganizationIds, organizationIds);
            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithSort(UserIndexType.Fields.EmailAddress, SortOrder.Ascending)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<CountResult> CountByOrganizationIdAsync(string organizationId) {
            var filter = Filter<User>.Term(u => u.OrganizationIds, new[] { organizationId });
            var options = new ExceptionlessQuery().WithElasticFilter(filter);
            return CountAsync(options);
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<User>> documents) {
            if (!IsCacheEnabled)
                return;

            var users = documents.Select(d => d.Value).Union(documents.Select(d => d.Original).Where(d => d != null)).ToList();
            foreach (var emailAddress in users.Select(u => u.EmailAddress).Distinct())
                await Cache.RemoveAsync(emailAddress).AnyContext();

            foreach (var organizationId in users.SelectMany(u => u.OrganizationIds).Distinct())
                await Cache.RemoveAsync("org:" + organizationId).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
