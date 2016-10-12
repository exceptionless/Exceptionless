using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(ExceptionlessElasticConfiguration configuration, IValidator<Token> validator)
            : base(configuration.Organizations.Token, validator) {
        }

        public Task<FindResults<Token>> GetByUserIdAsync(string userId) {
            var filter = Query<Token>.Term(e => e.UserId, userId);
            return FindAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, PagingOptions paging = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithElasticFilter(Query<Token>.Term(t => t.Type, type))
                .WithPaging(paging));
        }

        public Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, PagingOptions paging = null) {
            var filter = (
                    Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Query<Token>.Term(t => t.Type, type);

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging));
        }

        public override Task<FindResults<Token>> GetByProjectIdAsync(string projectId, PagingOptions paging = null) {
            var filter = (Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId));
            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithPaging(paging));
        }
    }
}