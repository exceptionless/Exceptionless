using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Core.Models;

public record OAuthAccount
{
    public static class KnownProviders
    {
        public const string GitHub = "github";
        public const string Google = "google";
        public const string Microsoft = "microsoft";
        public const string Facebook = "facebook";
        public const string WindowsLive = "windowslive";
    }

    [Required]
    [AllowedValues(KnownProviders.GitHub, KnownProviders.Google, KnownProviders.Microsoft, KnownProviders.Facebook, KnownProviders.WindowsLive,
        ErrorMessage = "The provider name must be github, google, microsoft, facebook, or windowslive.")]
    public required string Provider { get; init; }

    [Required]
    public required string ProviderUserId { get; init; }
    public required string Username { get; set; }

    public SettingsDictionary ExtraData { get; init; } = new();

    public string? EmailAddress()
    {
        if (!String.IsNullOrEmpty(Username) && Username.Contains("@"))
            return Username;

        foreach (var kvp in ExtraData)
        {
            if ((String.Equals(kvp.Key, "email") || String.Equals(kvp.Key, "account_email") || String.Equals(kvp.Key, "preferred_email") || String.Equals(kvp.Key, "personal_email")) && !String.IsNullOrEmpty(kvp.Value))
                return kvp.Value;
        }

        return null;
    }

    public string? FullName()
    {
        foreach (var kvp in ExtraData.Where(kvp => String.Equals(kvp.Key, "name") && !String.IsNullOrEmpty(kvp.Value)))
            return kvp.Value;

        return !String.IsNullOrEmpty(Username) && Username.Contains(" ") ? Username : null;
    }
}
