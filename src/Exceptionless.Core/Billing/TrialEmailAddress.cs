using System.Security.Cryptography;
using System.Text;

namespace Exceptionless.Core.Billing;

/// <summary>
/// Normalizes email addresses for the one-trial-per-mailbox rule and checks them against blocked disposable domains.
/// </summary>
public static class TrialEmailAddress
{
    private static readonly HashSet<string> s_dotInsensitiveDomains = new(StringComparer.Ordinal) { "gmail.com", "googlemail.com" };

    /// <summary>
    /// Disposable email providers blocked from trials until a system admin saves their own list.
    /// </summary>
    public static IReadOnlyList<string> DefaultBlockedDomains { get; } =
    [
        "10mail.org",
        "10minutemail.com",
        "10minutemail.de",
        "10minutemail.info",
        "10minutemail.net",
        "10minutemail.org",
        "10minutes.email",
        "1secmail.com",
        "1secmail.net",
        "1secmail.org",
        "1t-mail.com",
        "20mail.it",
        "20minutemail.com",
        "24mail.chacuo.net",
        "33mail.com",
        "binkmail.com",
        "byom.de",
        "chacuo.net",
        "crazymailing.com",
        "discard.email",
        "discardmail.com",
        "disposablemail.com",
        "dispostable.com",
        "dropmail.me",
        "emailfake.com",
        "emailnator.com",
        "emailondeck.com",
        "emltmp.com",
        "fake-mail.net",
        "fakeinbox.com",
        "fakemail.net",
        "femailtor.com",
        "getairmail.com",
        "getnada.com",
        "grr.la",
        "guerrillamail.biz",
        "guerrillamail.com",
        "guerrillamail.de",
        "guerrillamail.info",
        "guerrillamail.net",
        "guerrillamail.org",
        "guerrillamailblock.com",
        "harakirimail.com",
        "inboxkitten.com",
        "instaddr.ch",
        "instaddr.uk",
        "instaddr.win",
        "jourrapide.com",
        "kuku.lu",
        "linshiyou.com",
        "linshiyouxiang.net",
        "mail-temp.com",
        "mail.gw",
        "mail.tm",
        "mailcatch.com",
        "maildrop.cc",
        "mailinator.com",
        "mailinator.net",
        "mailinator.org",
        "mailinator2.com",
        "mailme24.com",
        "mailna.co",
        "mailna.me",
        "mailnesia.com",
        "mailsac.com",
        "mintemail.com",
        "minuteinbox.com",
        "minutemail.com",
        "moakt.cc",
        "moakt.com",
        "mohmal.com",
        "mohmal.in",
        "mytemp.email",
        "mytrashmail.com",
        "nada.email",
        "nada.ltd",
        "nimail.cn",
        "nospamfor.us",
        "one-time.email",
        "pookmail.com",
        "psnator.com",
        "rhyta.com",
        "safetymail.info",
        "sharklasers.com",
        "smartnator.com",
        "spamavert.com",
        "spambog.com",
        "spamcowboy.com",
        "spamgourmet.com",
        "spamherelots.com",
        "syinxun.com",
        "temp-mail.io",
        "temp-mail.org",
        "temp.email",
        "tempail.com",
        "tempforward.com",
        "tempinbox.com",
        "tempmail.cn",
        "tempmail.com",
        "tempmail.dev",
        "tempmail.net",
        "tempmail.org",
        "tempmail.plus",
        "tempmailo.com",
        "tempr.email",
        "throwaway.email",
        "throwawaymail.com",
        "tmail.ws",
        "tmailor.com",
        "tmpbox.net",
        "tmpeml.com",
        "tmpmail.net",
        "tmpmail.org",
        "tmpmailtor.com",
        "trash-mail.com",
        "trashmail.com",
        "trashmail.net",
        "trashmail.org",
        "trashmail.ws",
        "vtmpj.com",
        "vtmpj.net",
        "yopmail.com",
        "yopmail.fr",
        "yopmail.net",
        "youxiang.dev",
        "zhimail.xyz"
    ];

    /// <summary>
    /// Lowercases the address and removes the parts mail providers ignore: a <c>+tag</c> on any domain, and dots on
    /// Gmail, so aliases of one mailbox normalize to the same address.
    /// </summary>
    public static string Normalize(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        string address = emailAddress.Trim().ToLowerInvariant();
        int at = address.LastIndexOf('@');
        if (at is <= 0 || at == address.Length - 1)
            return address;

        string localPart = address[..at];
        string domain = address[(at + 1)..];

        int plus = localPart.IndexOf('+');
        if (plus is > 0)
            localPart = localPart[..plus];

        if (s_dotInsensitiveDomains.Contains(domain))
        {
            localPart = localPart.Replace(".", String.Empty);
            domain = "gmail.com";
        }

        return $"{localPart}@{domain}";
    }

    /// <summary>
    /// Identifies a mailbox using a dedicated HMAC secret. Protecting the normalized address's SHA-256 digest also
    /// allows existing digest-only claims to be migrated without recovering or retaining email addresses.
    /// </summary>
    public static string GetClaimId(string emailAddress, string secret)
    {
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(emailAddress))));
        return ProtectLegacyClaimId(digest, secret);
    }

    public static string ProtectLegacyClaimId(string digest, string secret)
    {
        byte[] key = Convert.FromBase64String(secret);
        if (key.Length is < 32)
            throw new ArgumentException("The trial claim secret must contain at least 32 random bytes", nameof(secret));

        return "hmac-v1-" + Convert.ToHexStringLower(HMACSHA256.HashData(key, Convert.FromHexString(digest)));
    }

    /// <summary>
    /// Whether the address is on a blocked domain or one of its subdomains.
    /// </summary>
    public static bool IsBlockedDomain(string emailAddress, IEnumerable<string> blockedDomains)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentNullException.ThrowIfNull(blockedDomains);

        string address = emailAddress.Trim().ToLowerInvariant();
        int at = address.LastIndexOf('@');
        if (at is < 0 || at == address.Length - 1)
            return false;

        string domain = address[(at + 1)..];
        return blockedDomains.Any(blocked => String.Equals(domain, blocked, StringComparison.Ordinal) || domain.EndsWith($".{blocked}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trims, lowercases, removes a leading <c>@</c>, drops blanks and duplicates, and sorts a domain list for saving.
    /// </summary>
    public static List<string> NormalizeDomains(IEnumerable<string>? domains)
        => (domains ?? [])
            .Select(domain => domain.Trim().TrimStart('@').ToLowerInvariant())
            .Where(domain => domain.Length is > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
}
