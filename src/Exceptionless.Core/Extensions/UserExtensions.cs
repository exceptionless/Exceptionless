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
        var accounts = user.OAuthAccounts.Where(account => String.Equals(account.Provider, providerName, StringComparison.Ordinal)
            && String.Equals(account.ProviderUserId, providerUserId, StringComparison.Ordinal)).ToArray();
        var account = accounts.FirstOrDefault();
        if (account is null)
        {
            account = new OAuthAccount
            {
                Provider = providerName,
                ProviderUserId = providerUserId,
                Username = username
            };
            user.OAuthAccounts.Add(account);
        }
        else
        {
            account.Username = username;
        }

        if (data is not null)
            account.ExtraData.Apply(data);

        foreach (var duplicate in accounts.Skip(1))
            user.OAuthAccounts.Remove(duplicate);
    }

    public static bool RemoveOAuthAccount(this User user, string providerName, string providerUserId)
    {
        var accounts = user.OAuthAccounts.Where(account => String.Equals(account.Provider, providerName, StringComparison.Ordinal)
            && String.Equals(account.ProviderUserId, providerUserId, StringComparison.Ordinal)).ToArray();
        if (accounts.Length is 0)
            return true;

        var activeOAuthAccounts = user.OAuthAccounts
            .Where(account => !String.Equals(account.Provider, OAuthAccount.KnownProviders.Facebook, StringComparison.OrdinalIgnoreCase)
                && !String.Equals(account.Provider, OAuthAccount.KnownProviders.WindowsLive, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (String.IsNullOrEmpty(user.Password) && activeOAuthAccounts.Length <= accounts.Length && activeOAuthAccounts.Contains(accounts[0]))
            return false;

        foreach (var account in accounts)
            user.OAuthAccounts.Remove(account);

        return true;
    }
}
