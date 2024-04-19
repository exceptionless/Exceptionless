using Exceptionless.Core.Models;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions;

public static class UserExtensions
{
    public static bool IsCorrectPassword(this User user, string password)
    {
        if (String.IsNullOrEmpty(user.Salt) || String.IsNullOrEmpty(user.Password))
            return false;

        string encodedPassword = password.ToSaltedHash(user.Salt);
        return String.Equals(encodedPassword, user.Password);
    }

    public static void ResetVerifyEmailAddressTokenAndExpiration(this User user)
    {
        user.IsEmailAddressVerified = false;
        user.VerifyEmailAddressToken = StringExtensions.GetNewToken();
        user.VerifyEmailAddressTokenExpiration = SystemClock.UtcNow.AddMinutes(1440);
    }

    public static void MarkEmailAddressVerified(this User user)
    {
        user.IsEmailAddressVerified = true;
        user.VerifyEmailAddressToken = null;
        user.VerifyEmailAddressTokenExpiration = DateTime.MinValue;
    }

    public static bool HasValidVerifyEmailAddressTokenExpiration(this User user)
    {
        return user.VerifyEmailAddressTokenExpiration != DateTime.MinValue && user.VerifyEmailAddressTokenExpiration >= SystemClock.UtcNow;
    }

    public static void ResetPasswordResetToken(this User user)
    {
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiration = DateTime.MinValue;
    }

    public static void CreatePasswordResetToken(this User user)
    {
        user.PasswordResetToken = StringExtensions.GetNewToken();
        user.PasswordResetTokenExpiration = SystemClock.UtcNow.AddMinutes(1440);
    }

    public static bool HasValidPasswordResetTokenExpiration(this User user)
    {
        return user.PasswordResetTokenExpiration != DateTime.MinValue && user.PasswordResetTokenExpiration >= SystemClock.UtcNow;
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
        if (user.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(user.Password))
            return false;

        var account = user.OAuthAccounts.FirstOrDefault(o => o.Provider == providerName.ToLowerInvariant() && o.ProviderUserId == providerUserId);
        if (account is null)
            return true;

        return user.OAuthAccounts.Remove(account);
    }
}
