using System.Diagnostics.CodeAnalysis;

namespace Exceptionless.Core.Models;

public record OAuthAccount
{
    public static class KnownProviders
    {
        public const string GitHub = "github";
        public const string Google = "google";
        public const string Facebook = "facebook";
        public const string WindowsLive = "windowslive";

        public static bool IsKnown([NotNullWhen(true)] string? providerName) => providerName is GitHub or Google or Facebook or WindowsLive;
    }

    public required string Provider { get; init; }
    public required string ProviderUserId { get; init; }
    public required string Username { get; init; }

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
