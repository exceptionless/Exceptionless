using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class OAuthTokenRepository : RepositoryBase<OAuthToken>, IOAuthTokenRepository
{
    private const int CleanupBatchSize = 500;

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

    public Task<FindResults<OAuthToken>> GetByGrantIdForUpdateAsync(string grantId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.GrantId, grantId).SortAscending(t => t.Id), options);
    }

    public Task<FindResults<OAuthToken>> GetByUserIdAsync(string userId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).SortDescending(t => t.UpdatedUtc), options);
    }

    public Task<FindResults<OAuthToken>> GetByUserIdAndClientIdAsync(string userId, string clientId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).FieldEquals(t => t.ClientId, clientId).SortDescending(t => t.UpdatedUtc), options);
    }

    public Task<FindResults<OAuthToken>> GetByUserIdAndClientIdForUpdateAsync(string userId, string clientId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return FindAsync(q => q.FieldEquals(t => t.UserId, userId).FieldEquals(t => t.ClientId, clientId).SortAscending(t => t.Id), options);
    }

    public async Task<long> RemoveExpiredDisabledAsync(DateTime utcCutoff, CancellationToken cancellationToken = default)
    {
        var expiredSpentTokenResults = await FindAsync(q => q
            .FieldEquals(t => t.IsDisabled, true)
            .DateRange(null, utcCutoff, (OAuthToken t) => t.RefreshExpiresUtc)
            .SortAscending(t => t.Id), o => o.ImmediateConsistency().SearchAfterPaging().PageLimit(CleanupBatchSize));

        long removed = await RemoveMatchingAsync(expiredSpentTokenResults, t => !String.IsNullOrEmpty(t.RefreshTokenHash));

        var clearedRefreshTokenResults = await FindAsync(q => q
            .FieldEquals(t => t.IsDisabled, true)
            .SortAscending(t => t.Id), o => o.ImmediateConsistency().SearchAfterPaging().PageLimit(CleanupBatchSize));

        removed += await RemoveMatchingAsync(clearedRefreshTokenResults, t => String.IsNullOrEmpty(t.RefreshTokenHash) && t.UpdatedUtc < utcCutoff);
        return removed;

        async Task<long> RemoveMatchingAsync(FindResults<OAuthToken> results, Func<OAuthToken, bool> shouldRemove)
        {
            long removedCount = 0;
            do
            {
                var tokensToRemove = results.Documents
                    .Where(shouldRemove)
                    .ToArray();

                if (tokensToRemove.Length > 0)
                {
                    await RemoveAsync(tokensToRemove, o => o.ImmediateConsistency());
                    removedCount += tokensToRemove.Length;
                }
            } while (!cancellationToken.IsCancellationRequested && await results.NextPageAsync());

            return removedCount;
        }
    }

    public Task<long> RemoveAllByUserIdAsync(string userId, CommandOptionsDescriptor<OAuthToken>? options = null)
    {
        return RemoveAllAsync(q => q.FieldEquals(t => t.UserId, userId), options);
    }
}
