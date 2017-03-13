using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories {
    public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository {
        public TokenRepository(ExceptionlessElasticConfiguration configuration, IValidator<Token> validator)
            : base(configuration.Organizations.Token, validator) {
        }

        public Task<FindResults<Token>> GetByUserIdAsync(string userId) {
            var filter = Query<Token>.Term(e => e.UserId, userId);
            return FindAsync(q => q.ElasticFilter(filter));
        }

        public Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, CommandOptionsDescriptor<Token> options = null) {
            return FindAsync(q => q.Organization(organizationId).ElasticFilter(Query<Token>.Term(t => t.Type, type)), options);
        }

        public Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, CommandOptionsDescriptor<Token> options = null) {
            var filter = (
                    Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId)
                ) && Query<Token>.Term(t => t.Type, type);

            return FindAsync(q => q.ElasticFilter(filter), options);
        }

        public override Task<FindResults<Token>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<Token> options = null) {
            var filter = (Query<Token>.Term(t => t.ProjectId, projectId) || Query<Token>.Term(t => t.DefaultProjectId, projectId));
            return FindAsync(q => q.ElasticFilter(filter), options);
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