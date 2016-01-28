using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(ElasticRepositoryContext<Token> context, OrganizationIndex index) : base(context, index) { }

        public Task<FindResults<Token>> GetApiTokensAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Query<Token>.Term(e => e.Type, TokenType.Access) && Query<Token>.Missing(e => e.UserId);
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("api-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByUserIdAsync(string userId) {
            var filter = Query<Token>.Term(e => e.UserId, userId);
            return FindAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithElasticFilter(Query<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdsAsync(TokenType type, ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<Token>());

            string cacheKey = String.Concat("type:", type, "-org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithElasticFilter(Query<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = (Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId)) && Query<Token>.Term(t => t.Type, type);

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("type:", type, "-project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdOrProjectIdAsync(TokenType type, string organizationId, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Query<Token>.And(and => (
                    Query<Token>.Term(t => t.OrganizationId, organizationId) || Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Query<Token>.Term(t => t.Type, type));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(String.Concat("type:", type, "-org:", organizationId, "-project:", projectId))
                .WithExpiresIn(expiresIn));
        }

        public Task<Token> GetByRefreshTokenAsync(string refreshToken) {
            if (String.IsNullOrEmpty(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));

            return FindOneAsync(new ExceptionlessQuery().WithElasticFilter(Query<Token>.Term(t => t.Refresh, refreshToken)));
        }

        public override Task<FindResults<Token>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Query<Token>.And(and => (Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId)));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<Token>> documents) {
            if (!IsCacheEnabled)
                return;

            foreach (var token in documents.Select(d => d.Value)) {
                await Cache.RemoveAsync(String.Concat("type:", token.Type, "-org:", token.OrganizationId)).AnyContext();
                await Cache.RemoveAsync(String.Concat("type:", token.Type, "-project:", token.ProjectId ?? token.DefaultProjectId)).AnyContext();
                await Cache.RemoveAsync(String.Concat("type:", token.Type, "-org:", token.OrganizationId, "-project:", token.ProjectId ?? token.DefaultProjectId)).AnyContext();
            }

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
