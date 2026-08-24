using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Core.Repositories;

public class UserRepository : RepositoryBase<User>, IUserRepository
{
    private readonly TimeProvider _timeProvider;

    public UserRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.Users, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        DefaultConsistency = Consistency.Immediate;
        AddRequiredField(u => u.EmailAddress, u => u.OrganizationIds);
    }

    public async Task<User?> GetByEmailAddressAsync(string emailAddress)
    {
        if (String.IsNullOrWhiteSpace(emailAddress))
            return null;

        emailAddress = emailAddress.Trim().ToLowerInvariant();
        var hit = await FindOneAsync(q => q.FieldEquals(u => u.EmailAddress, emailAddress), o => o.Cache(EmailCacheKey(emailAddress)));
        return hit?.Document;
    }

    public async Task<User?> GetByPasswordResetTokenAsync(string token)
    {
        if (String.IsNullOrEmpty(token))
            return null;

        var hit = await FindOneAsync(q => q.FieldEquals(u => u.PasswordResetToken, token));
        return hit?.Document;
    }

    public async Task<User?> GetUserByOAuthProviderAsync(string provider, string providerUserId)
    {
        if (String.IsNullOrEmpty(provider) || String.IsNullOrEmpty(providerUserId))
            return null;

        provider = provider.ToLowerInvariant();
        var results = (await FindAsync(q => q.FieldEquals(u => u.OAuthAccounts.First().ProviderUserId, providerUserId))).Documents;
        return results.FirstOrDefault(u => u.OAuthAccounts.Any(o => o.Provider == provider));
    }

    public async Task<User?> GetByVerifyEmailAddressTokenAsync(string token)
    {
        if (String.IsNullOrEmpty(token))
            return null;

        var hit = await FindOneAsync(q => q.FieldEquals(u => u.VerifyEmailAddressToken, token));
        return hit?.Document;
    }

    public Task<FindResults<User>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<User>? options = null)
    {
        if (String.IsNullOrEmpty(organizationId))
            return Task.FromResult(new FindResults<User>());

        var commandOptions = options.Configure();
        if (commandOptions.ShouldUseCache())
            throw new Exception("Caching of paged queries is not allowed");

        return FindAsync(q => q.FieldEquals(u => u.OrganizationIds, organizationId).SortAscending(u => u.EmailAddress), o => commandOptions);
    }

    public Task<FindResults<User>> GetByDefaultSavedViewIdAsync(string savedViewId, CommandOptionsDescriptor<User>? options = null)
    {
        if (String.IsNullOrEmpty(savedViewId))
            return Task.FromResult(new FindResults<User>());

        return FindAsync(q => q.FieldEquals(u => u.OrganizationPreferences.First().DefaultSavedViewId, savedViewId), options);
    }

    public Task<FindResults<User>> GetByPreferenceOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<User>? options = null)
    {
        if (String.IsNullOrEmpty(organizationId))
            return Task.FromResult(new FindResults<User>());

        return FindAsync(q => q.FieldEquals(u => u.OrganizationPreferences.First().OrganizationId, organizationId), options);
    }

    public Task<bool> SetDefaultSavedViewAsync(string userId, string organizationId, string? savedViewId)
    {
        const string script = @"
if (ctx._source.organization_preferences == null) {
  ctx._source.organization_preferences = [];
}

for (int i = ctx._source.organization_preferences.size() - 1; i >= 0; i--) {
  if (ctx._source.organization_preferences[i].organization_id == params.organizationId) {
    ctx._source.organization_preferences.remove(i);
  }
}

if (params.savedViewId != null) {
  ctx._source.organization_preferences.add([
    'organization_id': params.organizationId,
    'default_saved_view_id': params.savedViewId
  ]);
}

ctx._source.updated_utc = params.updatedUtc;";

        var operation = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>
            {
                ["organizationId"] = organizationId,
                ["savedViewId"] = savedViewId!,
                ["updatedUtc"] = _timeProvider.GetUtcNow().UtcDateTime
            }
        };

        return PatchAsync(userId, operation, o => o.ImmediateConsistency());
    }

    public Task<bool> RemoveDefaultSavedViewsAsync(string userId, IReadOnlyCollection<string> savedViewIds)
    {
        const string script = @"
if (ctx._source.organization_preferences != null) {
  for (int i = ctx._source.organization_preferences.size() - 1; i >= 0; i--) {
    if (params.savedViewIds.contains(ctx._source.organization_preferences[i].default_saved_view_id)) {
      ctx._source.organization_preferences.remove(i);
    }
  }
}

ctx._source.updated_utc = params.updatedUtc;";

        var operation = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>
            {
                ["savedViewIds"] = savedViewIds,
                ["updatedUtc"] = _timeProvider.GetUtcNow().UtcDateTime
            }
        };

        return PatchAsync(userId, operation, o => o.ImmediateConsistency());
    }

    protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<User>> findHits, ICommandOptions options, bool isDirtyRead)
    {
        await base.AddDocumentsToCacheAsync(findHits, options, isDirtyRead);

        var cacheEntries = new Dictionary<string, FindHit<User>>();
        foreach (var hit in findHits.Where(d => !String.IsNullOrEmpty(d.Document?.EmailAddress)))
            cacheEntries.Add(EmailCacheKey(hit.Document!.EmailAddress), hit);

        if (cacheEntries.Count > 0)
            await AddDocumentsToCacheWithKeyAsync(cacheEntries, options.GetExpiresIn());
    }

    protected override Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<User>> documents, ChangeType? changeType = null)
    {
        var keysToRemove = documents.UnionOriginalAndModified().Select(u => EmailCacheKey(u.EmailAddress)).Distinct();
        return Task.WhenAll(Cache.RemoveAllAsync(keysToRemove), base.InvalidateCacheAsync(documents, changeType));
    }

    private static string EmailCacheKey(string emailAddress) => String.Concat("Email:", emailAddress.Trim().ToLowerInvariant());
}
