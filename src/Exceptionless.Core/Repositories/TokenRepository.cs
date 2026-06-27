using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Token = Exceptionless.Core.Models.Token;

namespace Exceptionless.Core.Repositories;

public class TokenRepository : RepositoryOwnedByOrganizationAndProject<Token>, ITokenRepository
{
    public TokenRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
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
        return FindAsync(q => q
            .FieldOr(g => g
                .FieldEquals(t => t.ProjectId, projectId)
                .FieldEquals(t => t.DefaultProjectId, projectId))
            .FieldEquals(t => t.Type, (int)type)
            .Sort(f => f.CreatedUtc), options);
    }

    public Task<FindResults<Token>> GetByRefreshTokenAsync(string refreshToken, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.Refresh, refreshToken).SortDescending(f => f.CreatedUtc), options);
    }

    public Task<FindResults<Token>> GetOAuthAccessTokensByGrantIdAsync(string grantId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.OAuthGrantId, grantId).FieldEquals(t => t.Type, (int)TokenType.Access).FieldEquals(t => t.OAuthType, (int)OAuthTokenType.Access).SortDescending(f => f.UpdatedUtc), options);
    }

    public Task<FindResults<Token>> GetOAuthAccessTokensByUserIdAsync(string userId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).FieldEquals(t => t.Type, (int)TokenType.Access).FieldEquals(t => t.OAuthType, (int)OAuthTokenType.Access).SortDescending(f => f.UpdatedUtc), options);
    }

    public Task<FindResults<Token>> GetOAuthAccessTokensByUserIdAndClientIdAsync(string userId, string clientId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).FieldEquals(t => t.OAuthClientId, clientId).FieldEquals(t => t.Type, (int)TokenType.Access).FieldEquals(t => t.OAuthType, (int)OAuthTokenType.Access).SortDescending(f => f.UpdatedUtc), options);
    }

    public override Task<FindResults<Token>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q
            .FieldOr(g => g
                .FieldEquals(t => t.ProjectId, projectId)
                .FieldEquals(t => t.DefaultProjectId, projectId))
            .Sort(f => f.CreatedUtc), options);
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
