using Exceptionless.Core.Authorization;
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

    public async Task<Token?> GetForAuthenticationAsync(string tokenId)
    {
        if (String.IsNullOrEmpty(tokenId))
            return null;

        if (IsCacheEnabled)
        {
            var cached = (await GetCachedFindHit(new Id(tokenId)))?.Document;
            if (cached is { Type: TokenType.Access } && String.IsNullOrEmpty(cached.UserId))
                return cached;
        }

        // Session state must come from storage even if an older read repopulates the cache.
        var token = await GetByIdAsync(tokenId, o => o.Cache(false));
        if (token is { Type: TokenType.Access } && String.IsNullOrEmpty(token.UserId))
            await AddDocumentsToCacheAsync(token, new CommandOptions<Token>().Cache(), false);

        return token;
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

    public Task<FindResults<Token>> GetDefaultClientTokenAsync(string projectId, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q
            .FieldOr(g => g
                .FieldEquals(t => t.ProjectId, projectId)
                .FieldEquals(t => t.DefaultProjectId, projectId))
            .FieldEquals(t => t.Type, (int)TokenType.Access)
            .FieldOr(g => g
                .FieldEquals(t => t.Scopes, AuthorizationRoles.Client)
                .FieldEquals(t => t.Scopes, null!))
            .Sort(f => f.CreatedUtc), options);
    }

    public Task<FindResults<Token>> GetByRefreshTokenAsync(string refreshToken, CommandOptionsDescriptor<Token>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.Refresh, refreshToken).SortDescending(f => f.CreatedUtc), options);
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
