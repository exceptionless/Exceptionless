using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IUserRepository : ISearchableRepository<User>
{
    Task<User?> GetByEmailAddressAsync(string emailAddress);
    Task<User?> GetByPasswordResetTokenAsync(string token);
    Task<User?> GetUserByOAuthProviderAsync(string provider, string providerUserId);
    Task<User?> GetByVerifyEmailAddressTokenAsync(string token);
    Task<FindResults<User>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<User>? options = null);
    Task<FindResults<User>> GetByDefaultSavedViewIdAsync(string savedViewId, CommandOptionsDescriptor<User>? options = null);
    Task<bool> SetDefaultSavedViewAsync(string userId, string organizationId, string? savedViewId);
    Task<bool> RemoveDefaultSavedViewsAsync(string userId, IReadOnlyCollection<string> savedViewIds);
}
