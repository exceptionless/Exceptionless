using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IOAuthTokenRepository : ISearchableRepository<OAuthToken>
{
    Task<FindResults<OAuthToken>> GetByAccessTokenHashAsync(string accessTokenHash, CommandOptionsDescriptor<OAuthToken>? options = null);
    Task<FindResults<OAuthToken>> GetByRefreshTokenHashAsync(string refreshTokenHash, CommandOptionsDescriptor<OAuthToken>? options = null);
    Task<FindResults<OAuthToken>> GetByGrantIdAsync(string grantId, CommandOptionsDescriptor<OAuthToken>? options = null);
    Task<FindResults<OAuthToken>> GetByUserIdAsync(string userId, CommandOptionsDescriptor<OAuthToken>? options = null);
    Task<FindResults<OAuthToken>> GetByUserIdAndClientIdAsync(string userId, string clientId, CommandOptionsDescriptor<OAuthToken>? options = null);
    Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<OAuthToken>? options = null);
}
