using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services;

public sealed class PasswordService(IUserRepository userRepository)
{
    public async Task<User?> AuthenticateAsync(User user, string password)
    {
        // Email lookups can contain snapshots populated after a concurrent write
        // invalidated the cache. Credentials and claims must come from storage.
        var current = await userRepository.GetByIdAsync(user.Id, o => o.Cache(false));
        if (current is not { IsActive: true } ||
            !String.Equals(current.EmailAddress, user.EmailAddress, StringComparison.OrdinalIgnoreCase) ||
            !current.IsCorrectPassword(password))
            return null;
        if (!PasswordHasher.NeedsUpgrade(current.Password!))
            return current;
        string salt = PasswordHasher.CreateSalt();
        var updated = await userRepository.UpgradePasswordHashAsync(current, salt, PasswordHasher.Hash(password, salt));
        // A concurrent password change or account update wins over the observed record.
        return updated is { IsActive: true } &&
            String.Equals(updated.EmailAddress, user.EmailAddress, StringComparison.OrdinalIgnoreCase) &&
            updated.IsCorrectPassword(password) ? updated : null;
    }
}
