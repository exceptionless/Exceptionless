using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Core.Repositories {
    public class UserRepository : RepositoryBase<User>, IUserRepository {
        public UserRepository(ExceptionlessElasticConfiguration configuration, IValidator<User> validator)
            : base(configuration.Organizations.User, validator) {
            FieldsRequiredForRemove.AddRange(new [] { ElasticType.GetFieldName(u => u.EmailAddress), ElasticType.GetFieldName(u => u.OrganizationIds) });
            DocumentsAdded.AddHandler(OnDocumentsAdded);
        }

        public async Task<User> GetByEmailAddressAsync(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.ToLowerInvariant().Trim();
            var query = new ExceptionlessQuery()
                .WithElasticFilter(Query<User>.Term(u => u.EmailAddress.Suffix("keyword"), emailAddress))
                .WithCacheKey(String.Concat("Email:", emailAddress));

            var hit = await FindOneAsync(query).AnyContext();
            return hit?.Document;
        }

        public async Task<User> GetByPasswordResetTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var hit = await FindOneAsync(new ExceptionlessQuery().WithElasticFilter(Query<User>.Term(u => u.PasswordResetToken, token))).AnyContext();
            return hit?.Document;
        }

        public async Task<User> GetUserByOAuthProviderAsync(string provider, string providerUserId) {
            if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
                return null;

            provider = provider.ToLowerInvariant();
            var filter = Query<User>.Term(u => u.OAuthAccounts.First().ProviderUserId, providerUserId);
            var results = (await FindAsync(new ExceptionlessQuery().WithElasticFilter(filter)).AnyContext()).Documents;
            return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
        }

        public async Task<User> GetByVerifyEmailAddressTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Query<User>.Term(u => u.VerifyEmailAddressToken, token);
            var hit = await FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter)).AnyContext();
            return hit?.Document;
        }

        public Task<FindResults<User>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(organizationId))
                return Task.FromResult<FindResults<User>>(new FindResults<User>());

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(Query<User>.Term(u => u.OrganizationIds, organizationId))
                .WithPaging(paging)
                .WithSortAscending((User u) => u.EmailAddress.Suffix("keyword"))
                .WithCacheKey(useCache ? String.Concat("paged:Organization:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<User>> documents) {
            if (!IsCacheEnabled)
                return;

            var users = documents.UnionOriginalAndModified();
            var keysToRemove = users.Select(u => String.Concat("Email:", u.EmailAddress.ToLowerInvariant().Trim())).Distinct().ToList();
            await Cache.RemoveAllAsync(keysToRemove).AnyContext();

            await InvalidateCachedQueriesAsync(users).AnyContext();
            await base.InvalidateCacheAsync(documents).AnyContext();
        }

        private Task OnDocumentsAdded(object sender, DocumentsEventArgs<User> documents) {
            if (!IsCacheEnabled)
                return Task.CompletedTask;

            return InvalidateCachedQueriesAsync(documents.Documents);
        }

        protected virtual async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<User> documents) {
            var organizations = documents.SelectMany(d => d.OrganizationIds).Distinct().Where(id => !String.IsNullOrEmpty(id));
            foreach (var organizationId in organizations)
                await Cache.RemoveByPrefixAsync($"paged:Organization:{organizationId}:*").AnyContext();
        }
    }
}