using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : RepositoryBase<User>, IUserRepository {
        public UserRepository(ExceptionlessElasticConfiguration configuration, IValidator<User> validator) 
            : base(configuration.Organizations.User, validator) {}

        public async Task<User> GetByEmailAddressAsync(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.ToLowerInvariant().Trim();
            var filter = Filter<User>.Term(u => u.EmailAddress, emailAddress);
            var hit = await FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter).WithCacheKey(String.Concat("Email:", emailAddress))).AnyContext();
            return hit?.Document;
        }

        public async Task<User> GetByPasswordResetTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.PasswordResetToken, token);
            var hit = await FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter)).AnyContext();
            return hit?.Document;
        }

        public async Task<User> GetUserByOAuthProviderAsync(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();

            var filter = Filter<User>.Term(UserIndexType.Fields.OAuthAccountProviderUserId, new List<string>() { providerUserId });
            var results = (await FindAsync(new ExceptionlessQuery().WithElasticFilter(filter)).AnyContext()).Documents;

            return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
        }

        public async Task<User> GetByVerifyEmailAddressTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<User>.Term(u => u.VerifyEmailAddressToken, token);
            var hit = await FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter)).AnyContext();
            return hit?.Document;
        }

        public Task<IFindResults<User>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(organizationId))
                return Task.FromResult<IFindResults<User>>(new FindResults<User>());

            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithPaging(paging)
                .WithSort(UserIndexType.Fields.EmailAddress, SortOrder.Ascending)
                .WithCacheKey(useCache ? String.Concat("Organization:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<User>> documents) {
            if (!IsCacheEnabled)
                return;

            var users = documents.Select(d => d.Value).Union(documents.Select(d => d.Original).Where(d => d != null)).ToList();
            var emailKeys = users.Select(u => String.Concat("Email:", u.EmailAddress.ToLowerInvariant().Trim())).ToList();
            await Cache.RemoveAllAsync(emailKeys).AnyContext();

            var organizationKeys = users.Select(u => String.Concat("Organization:", u.OrganizationIds)).ToList();
            await Cache.RemoveAllAsync(organizationKeys).AnyContext();
            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
