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
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(ExceptionlessElasticConfiguration configuration, IValidator<Token> validator) 
            : base(configuration.Organizations.Token, validator) {
            FieldsRequiredForRemove.Add("type");
        }

        public Task<IFindResults<Token>> GetByUserIdAsync(string userId) {
            var filter = Filter<Token>.Term(e => e.UserId, userId);
            return FindAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public Task<IFindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithElasticFilter(Filter<Token>.Term(t => t.Type, type))
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("paged:Type:", type, ":Organization:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<IFindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (
                    Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Filter<Token>.Term(t => t.Type, type));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("paged:Type:", type, ":Project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public override Task<IFindResults<Token>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            var filter = Filter<Token>.And(and => (Filter<Token>.Term(t => t.ProjectId, projectId) || Filter<Token>.Term(t => t.DefaultProjectId, projectId)));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("paged:Project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        protected override async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<Token> documents) {
            var keysToRemove = documents.SelectMany(d => {
                var list = new List<string>(3);
                if (!String.IsNullOrEmpty(d.OrganizationId))
                    list.Add(String.Concat("paged:Type:", d.Type, ":Organization:", d.OrganizationId, ":*"));

                if (!String.IsNullOrEmpty(d.ProjectId)) {
                    list.Add(String.Concat("paged:Project:", d.ProjectId, ":*"));
                    list.Add(String.Concat("paged:Type:", d.Type, ":Project:", d.ProjectId, ":*"));
                } else if (!String.IsNullOrEmpty(d.DefaultProjectId)) {
                    list.Add(String.Concat("paged:Project:", d.DefaultProjectId, ":*"));
                    list.Add(String.Concat("paged:Type:", d.Type, ":Project:", d.DefaultProjectId, ":*"));
                }

                return list;
            }).Distinct().ToList();

            foreach (var key in keysToRemove)
                await Cache.RemoveByPrefixAsync(key).AnyContext();

            await base.InvalidateCachedQueriesAsync(documents).AnyContext();
        }
    }
}