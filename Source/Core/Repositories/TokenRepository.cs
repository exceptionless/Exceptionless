using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Token> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public Task<FindResults<Token>> GetApiTokensAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.Term(e => e.Type, TokenType.Access) && Filter<Token>.Missing(e => e.UserId);
            return FindAsync(new ElasticSearchOptions<Token>()
                .WithOrganizationId(organizationId)
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("api-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByUserIdAsync(string userId) {
            var filter = Filter<Token>.Term(e => e.UserId, userId);
            return FindAsync(new ElasticSearchOptions<Token>().WithFilter(filter));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ElasticSearchOptions<Token>()
                .WithOrganizationId(organizationId)
                .WithFilter(Filter<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdsAsync(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<Token>());

            string cacheKey = String.Concat("type:", type, "-org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ElasticSearchOptions<Token>()
                .WithOrganizationIds(organizationIds)
                .WithFilter(Filter<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return FindAsync(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdOrProjectIdAsync(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.OrganizationId, organizationId) || Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return FindAsync(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(String.Concat("type:", type, "-org:", organizationId, "-project:", projectId))
                .WithExpiresIn(expiresIn));
        }

        public Task<Token> GetByRefreshTokenAsync(string refreshToken) {
            if (String.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));

            return FindOneAsync(new ElasticSearchOptions<Token>().WithFilter(Filter<Token>.Term(t => t.Refresh, refreshToken)));
        }

        public override Task<FindResults<Token>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)));

            return FindAsync(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }
        
        protected override async Task InvalidateCacheAsync(ICollection<Token> tokens, ICollection<Token> originalTokens) {
            if (!EnableCache)
                return;

            foreach (var token in tokens) {
                await InvalidateCacheAsync(String.Concat("type:", token.Type, "-org:", token.OrganizationId)).AnyContext();
                await InvalidateCacheAsync(String.Concat("type:", token.Type, "-project:", token.ProjectId ?? token.DefaultProjectId)).AnyContext();
                await InvalidateCacheAsync(String.Concat("type:", token.Type, "-org:", token.OrganizationId, "-project:", token.ProjectId ?? token.DefaultProjectId)).AnyContext();
            }

            await base.InvalidateCacheAsync(tokens, originalTokens).AnyContext();
        }
    }
}