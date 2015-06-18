using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : ElasticSearchRepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Token> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public FindResults<Token> GetApiTokens(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.Term(e => e.Type, TokenType.Access) && Filter<Token>.Missing(e => e.UserId);
            return Find(new ElasticSearchOptions<Token>()
                .WithOrganizationId(organizationId)
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("api-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByUserId(string userId) {
            var filter = Filter<Token>.Term(e => e.UserId, userId);
            return Find(new ElasticSearchOptions<Token>().WithFilter(filter));
        }

        public FindResults<Token> GetByTypeAndOrganizationId(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find(new ElasticSearchOptions<Token>()
                .WithOrganizationId(organizationId)
                .WithFilter(Filter<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByTypeAndOrganizationIds(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new FindResults<Token>();

            string cacheKey = String.Concat("type:", type, "-org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find(new ElasticSearchOptions<Token>()
                .WithOrganizationIds(organizationIds)
                .WithFilter(Filter<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByTypeAndProjectId(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return Find(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public FindResults<Token> GetByTypeAndOrganizationIdOrProjectId(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.OrganizationId, organizationId) || Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return Find(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(String.Concat("type:", type, "-org:", organizationId, "-project:", projectId))
                .WithExpiresIn(expiresIn));
        }

        public Token GetByRefreshToken(string refreshToken) {
            if (String.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException("refreshToken");

            return FindOne(new ElasticSearchOptions<Token>().WithFilter(Filter<Token>.Term(t => t.Refresh, refreshToken)));
        }

        public override FindResults<Token> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)));

            return Find(new ElasticSearchOptions<Token>()
                .WithFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }
        
        protected override void InvalidateCache(ICollection<Token> tokens, ICollection<Token> originalTokens) {
            if (!EnableCache)
                return;

            foreach (var token in tokens) {
                InvalidateCache(String.Concat("type:", token.Type, "-org:", token.OrganizationId));
                InvalidateCache(String.Concat("type:", token.Type, "-project:", token.ProjectId ?? token.DefaultProjectId));
                InvalidateCache(String.Concat("type:", token.Type, "-org:", token.OrganizationId, "-project:", token.ProjectId ?? token.DefaultProjectId));
            }

            base.InvalidateCache(tokens, originalTokens);
        }
    }
}