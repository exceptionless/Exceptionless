using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Services;

public sealed class PasswordService(IUserRepository userRepository)
{
    public async Task<User?> AuthenticateAsync(User user, string password)
    {
        if (!user.IsActive || !user.IsCorrectPassword(password))
            return null;
        if (!PasswordHasher.NeedsUpgrade(user.Password!))
            return user;
        string salt = PasswordHasher.CreateSalt();
        var updated = await userRepository.UpgradePasswordHashAsync(user, salt, PasswordHasher.Hash(password, salt));
        // A concurrent password change or account update wins over the observed record.
        return updated is { IsActive: true } && updated.IsCorrectPassword(password) ? updated : null;
    }
}
