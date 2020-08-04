using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Nest;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : RepositoryBase<User>, IUserRepository {
        public UserRepository(ExceptionlessElasticConfiguration configuration, IValidator<User> validator, AppOptions options)
            : base(configuration.Users, validator, options) {
            AddPropertyRequiredForRemove(u => u.EmailAddress, u => u.OrganizationIds);
        }

        public async Task<User> GetByEmailAddressAsync(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.Trim().ToLowerInvariant();
            var hit = await FindOneAsync(q => q.ElasticFilter(Query<User>.Term(u => u.EmailAddress.Suffix("keyword"), emailAddress)), o => o.CacheKey(EmailCacheKey(emailAddress))).AnyContext();
            return hit?.Document;
        }

        public async Task<User> GetByPasswordResetTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var hit = await FindOneAsync(q => q.ElasticFilter(Query<User>.Term(u => u.PasswordResetToken, token))).AnyContext();
            return hit?.Document;
        }

        public async Task<User> GetUserByOAuthProviderAsync(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();
            var filter = Query<User>.Term(u => u.OAuthAccounts.First().ProviderUserId, providerUserId);
            var results = (await FindAsync(q => q.ElasticFilter(filter)).AnyContext()).Documents;
            return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
        }

        public async Task<User> GetByVerifyEmailAddressTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Query<User>.Term(u => u.VerifyEmailAddressToken, token);
            var hit = await FindOneAsync(q => q.ElasticFilter(filter)).AnyContext();
            return hit?.Document;
        }

        public Task<FindResults<User>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<User> options = null) {
            if (String.IsNullOrEmpty(organizationId))
                return Task.FromResult(new FindResults<User>());

            var commandOptions = options.Configure();
            if (commandOptions.ShouldUseCache())
                throw new Exception("Caching of paged queries is not allowed");

            var filter = Query<User>.Term(u => u.OrganizationIds, organizationId);
            return FindAsync(q => q.ElasticFilter(filter).SortAscending(u => u.EmailAddress.Suffix("keyword")), o => commandOptions);
        }

        protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<User>> findHits, ICommandOptions options) {
            await base.AddDocumentsToCacheAsync(findHits, options).AnyContext();

            var cacheEntries = new Dictionary<string, FindHit<User>>();
            foreach (var hit in findHits.Where(d => !String.IsNullOrEmpty(d.Document?.EmailAddress)))
                cacheEntries.Add(EmailCacheKey(hit.Document.EmailAddress), hit);

            if (cacheEntries.Count > 0)
                await AddDocumentsToCacheWithKeyAsync(cacheEntries, options.GetExpiresIn()).AnyContext();
        }

        protected override Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<User>> documents, ChangeType? changeType = null) {
            var keysToRemove = documents.UnionOriginalAndModified().Select(u => EmailCacheKey(u.EmailAddress)).Distinct();
            return Task.WhenAll(Cache.RemoveAllAsync(keysToRemove), base.InvalidateCacheAsync(documents, changeType));
        }
        
        private string EmailCacheKey(string emailAddress) => String.Concat("Email:", emailAddress.Trim().ToLowerInvariant());
    }
}