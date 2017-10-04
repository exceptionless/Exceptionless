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
        public UserRepository(ExceptionlessElasticConfiguration configuration, IValidator<User> validator)
            : base(configuration.Organizations.User, validator) {
            FieldsRequiredForRemove.AddRange(new Field[] { ElasticType.GetPropertyName(u => u.EmailAddress), ElasticType.GetPropertyName(u => u.OrganizationIds) });
            DocumentsAdded.AddHandler(OnDocumentsAdded);
        }

        public async Task<User> GetByEmailAddressAsync(string emailAddress) {
            if (String.IsNullOrWhiteSpace(emailAddress))
                return null;

            emailAddress = emailAddress.Trim().ToLowerInvariant();
            var hit = await FindOneAsync(q => q.ElasticFilter(Query<User>.Term(u => u.EmailAddress.Suffix("keyword"), emailAddress)), o => o.CacheKey(String.Concat("Email:", emailAddress))).AnyContext();
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
                return Task.FromResult<FindResults<User>>(new FindResults<User>());

            var commandOptions = options.Configure();
            if (commandOptions.ShouldUseCache())
                throw new Exception("Caching of paged queries is not allowed");

            var filter = Query<User>.Term(u => u.OrganizationIds, organizationId);
            return FindAsync(q => q.ElasticFilter(filter).SortAscending(u => u.EmailAddress.Suffix("keyword")), o => commandOptions);
        }

        protected override Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<User>> documents, ICommandOptions options = null) {
            if (!IsCacheEnabled)
                return Task.CompletedTask;

            var users = documents.UnionOriginalAndModified();
            var keysToRemove = users.Select(u => String.Concat("Email:", u.EmailAddress.ToLowerInvariant().Trim())).Distinct().ToList();
            return Task.WhenAll(
                Cache.RemoveAllAsync(keysToRemove),
                InvalidateCachedQueriesAsync(users, options),
                base.InvalidateCacheAsync(documents, options)
            );
        }

        private Task OnDocumentsAdded(object sender, DocumentsEventArgs<User> documents) {
            if (!IsCacheEnabled)
                return Task.CompletedTask;

            return InvalidateCachedQueriesAsync(documents.Documents, documents.Options);
        }

        protected virtual Task InvalidateCachedQueriesAsync(IReadOnlyCollection<User> documents, ICommandOptions options = null) {
            return Task.CompletedTask;
        }
    }
}