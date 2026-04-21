using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Token = Exceptionless.Core.Models.Token;
using ElasticInfer = Elastic.Clients.Elasticsearch.Infer;

namespace Exceptionless.Core.Repositories;

public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository
{
    public TokenRepository(ExceptionlessElasticConfiguration configuration, IValidator<Token> validator, AppOptions options)
        : base(configuration.Tokens, validator, options)
    {
        DefaultConsistency = Consistency.Immediate;
    }

    public Task<FindResults<Token>> GetByTypeAndUserIdAsync(TokenType type, string userId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).FieldEquals(t => t.Type, (int)type).Sort(f => f.CreatedUtc), options);
    }

    public Task<FindResults<Token>> GetByTypeAndOrganizationIdAsync(TokenType type, string organizationId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(t => t.Type, (int)type)
            .Sort(f => f.CreatedUtc), options);
    }

    public Task<FindResults<Token>> GetByTypeAndProjectIdAsync(TokenType type, string projectId, CommandOptionsDescriptor<Token>? options = null)
    {
        Query filter = new BoolQuery
        {
            Should = [
                new TermQuery { Field = ElasticInfer.Field<Token>(t => t.ProjectId), Value = projectId },
                new TermQuery { Field = ElasticInfer.Field<Token>(t => t.DefaultProjectId), Value = projectId }
            ],
            MinimumShouldMatch = 1
        };
        return FindAsync(q => q.ElasticFilter(filter).FieldEquals(t => t.Type, (int)type).Sort(f => f.CreatedUtc), options);
    }

    public override Task<FindResults<Token>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<Token>? options = null)
    {
        Query filter = new BoolQuery
        {
            Should = [
                new TermQuery { Field = ElasticInfer.Field<Token>(t => t.ProjectId), Value = projectId },
                new TermQuery { Field = ElasticInfer.Field<Token>(t => t.DefaultProjectId), Value = projectId }
            ],
            MinimumShouldMatch = 1
        };
        return FindAsync(q => q.ElasticFilter(filter).Sort(f => f.CreatedUtc), options);
    }

    public Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<Token>? options = null)
    {
        return RemoveAllAsync(q => q.FieldEquals(t => t.UserId, userId), options);
    }

    protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, Token? document, IDictionary<string, object?>? data = null, TimeSpan? delay = null)
    {
        var items = new Foundatio.Utility.DataDictionary(data ?? new Dictionary<string, object?>())
        {
            { ExtendedEntityChanged.KnownKeys.IsAuthenticationToken, TokenType.Authentication == document?.Type },
            { ExtendedEntityChanged.KnownKeys.UserId, document?.UserId },
        };

        return PublishMessageAsync(CreateEntityChanged(changeType, document?.OrganizationId, document?.ProjectId ?? document?.DefaultProjectId, null, document?.Id, items), delay);
    }
}
