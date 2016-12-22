using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
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

        protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, Token document, IDictionary<string, object> data = null, TimeSpan? delay = null) {
            return PublishMessageAsync(new ExtendedEntityChanged {
                ChangeType = changeType,
                Id = document?.Id,
                OrganizationId = document?.OrganizationId,
                ProjectId = document?.ProjectId ?? document?.DefaultProjectId,
                Type = EntityTypeName,
                Data = new Foundatio.Utility.DataDictionary(data ?? new Dictionary<string, object>()) {
                    { "IsAuthenticationToken", TokenType.Authentication == document?.Type  },
                    { "UserId", document?.UserId }
                }
            }, delay);
        }
    }
}