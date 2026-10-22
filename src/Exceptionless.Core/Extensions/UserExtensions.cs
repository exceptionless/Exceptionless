using System.Diagnostics.CodeAnalysis;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;

namespace Exceptionless.Core.Extensions;

public static class UserExtensions
{
    /// <summary>
    /// Checks whether an active user's session generation matches the presented credential.
    /// A missing generation matches only another missing generation for legacy credentials.
    /// This method does not load or refresh the user.
    /// </summary>
    public static bool HasValidAuthenticationVersion([NotNullWhen(true)] this User? user, string? authenticationVersion)
    {
        return user is { IsActive: true }
            && String.Equals(user.AuthenticationVersion, authenticationVersion, StringComparison.Ordinal);
    }

    public static bool IsCorrectPassword(this User user, string password)
    {
        if (String.IsNullOrEmpty(user.Salt) || String.IsNullOrEmpty(user.Password))
            return false;

        string encodedPassword = password.ToSaltedHash(user.Salt);
        return String.Equals(encodedPassword, user.Password);
    }

    public static void ResetVerifyEmailAddressTokenAndExpiration(this User user, TimeProvider timeProvider)
    {
        user.IsEmailAddressVerified = false;
        user.VerifyEmailAddressToken = StringExtensions.GetNewToken();
        user.VerifyEmailAddressTokenExpiration = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1440);
    }

    public static void MarkEmailAddressVerified(this User user)
    {
        user.IsEmailAddressVerified = true;
        user.VerifyEmailAddressToken = null;
        user.VerifyEmailAddressTokenExpiration = DateTime.MinValue;
    }

    public static bool HasValidVerifyEmailAddressTokenExpiration(this User user, TimeProvider timeProvider)
    {
        return user.VerifyEmailAddressTokenExpiration.IsAfterOrEqual(timeProvider.GetUtcNow().UtcDateTime);
    }

    public static void ResetPasswordResetToken(this User user)
    {
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiration = DateTime.MinValue;
    }

    public static void CreatePasswordResetToken(this User user, TimeProvider timeProvider)
    {
        user.PasswordResetToken = StringExtensions.GetNewToken();
        user.PasswordResetTokenExpiration = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1440);
    }

    public static bool HasValidPasswordResetTokenExpiration(this User user, TimeProvider timeProvider)
    {
        return user.PasswordResetTokenExpiration != DateTime.MinValue && user.PasswordResetTokenExpiration >= timeProvider.GetUtcNow().UtcDateTime;
    }

    public static void AddOAuthAccount(this User user, string providerName, string providerUserId, string username, SettingsDictionary? data = null)
    {
        var account = new OAuthAccount
        {
            Provider = providerName.ToLowerInvariant(),
            ProviderUserId = providerUserId,
            Username = username
        };

        if (data is not null)
            account.ExtraData.Apply(data);

        user.OAuthAccounts.Add(account);
    }

    public static bool RemoveOAuthAccount(this User user, string providerName, string providerUserId)
    {
        var account = user.OAuthAccounts.FirstOrDefault(o => String.Equals(o.Provider, providerName, StringComparison.OrdinalIgnoreCase) && o.ProviderUserId == providerUserId);
        if (account is null)
            return true;

        // Retired provider records cannot keep a passwordless account accessible.
        if (String.IsNullOrEmpty(user.Password)
            && !String.Equals(account.Provider, "facebook", StringComparison.OrdinalIgnoreCase)
            && !String.Equals(account.Provider, "windowslive", StringComparison.OrdinalIgnoreCase)
            && !user.OAuthAccounts.Any(other => other != account
                && !String.Equals(other.Provider, "facebook", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(other.Provider, "windowslive", StringComparison.OrdinalIgnoreCase)))
            return false;

        return user.OAuthAccounts.Remove(account);
    }
}
