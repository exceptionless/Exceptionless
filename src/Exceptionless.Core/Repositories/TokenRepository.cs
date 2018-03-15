using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
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

        public Task<FindResults<Token>> GetByTypeAndUserIdAsync(TokenType type, string userId, CommandOptionsDescriptor<Token> options = null) {
            var filter = Query<Token>.Term(e => e.UserId, userId) && Query<Token>.Term(t => t.Type, type);
            return FindAsync(q => q.ElasticFilter(filter), options);
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

        public Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<Token> options = null) {
            return RemoveAllAsync(q => q.ElasticFilter(Query<Token>.Term(t => t.UserId, userId)), options);
        }

        protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, Token document, IDictionary<string, object> data = null, TimeSpan? delay = null) {
            var items = new Foundatio.Utility.DataDictionary(data ?? new Dictionary<string, object>()) {
                { ExtendedEntityChanged.KnownKeys.IsAuthenticationToken, TokenType.Authentication == document?.Type },
                { ExtendedEntityChanged.KnownKeys.UserId, document?.UserId }
            };
            return PublishMessageAsync(CreateEntityChanged(changeType, document?.OrganizationId, document?.ProjectId ?? document?.DefaultProjectId, null, document?.Id, items), delay);
        }
    }
}