using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class OAuthTokenRepository : RepositoryBase<OAuthToken>, IOAuthTokenRepository
{
    public OAuthTokenRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.OAuthTokens, validator, options)
    {
        DefaultConsistency = Consistency.Immediate;
    }

    public Task<FindResults<OAuthToken>> GetByAccessTokenHashAsync(string accessTokenHash, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.AccessTokenHash, accessTokenHash), options);
    }

    public Task<FindResults<OAuthToken>> GetByRefreshTokenHashAsync(string refreshTokenHash, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q
            .FieldEquals(t => t.RefreshTokenHash, refreshTokenHash)
            .SortDescending(t => t.CreatedUtc), options);
    }

    public Task<FindResults<OAuthToken>> GetByGrantIdAsync(string grantId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.GrantId, grantId).SortDescending(t => t.UpdatedUtc), options);
    }

    public Task<FindResults<OAuthToken>> GetByUserIdAsync(string userId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).SortDescending(t => t.UpdatedUtc), options);
    }

    public Task<FindResults<OAuthToken>> GetByUserIdAndClientIdAsync(string userId, string clientId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).FieldEquals(t => t.ClientId, clientId).SortDescending(t => t.UpdatedUtc), options);
    }

    public Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return RemoveAllAsync(q => q.FieldEquals(t => t.UserId, userId), options);
    }
}
