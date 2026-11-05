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
        "10minutemail.com",
        "33mail.com",
        "dispostable.com",
        "emailondeck.com",
        "fakeinbox.com",
        "getairmail.com",
        "getnada.com",
        "guerrillamail.biz",
        "guerrillamail.com",
        "guerrillamail.de",
        "guerrillamail.info",
        "guerrillamail.net",
        "guerrillamail.org",
        "guerrillamailblock.com",
        "maildrop.cc",
        "mailinator.com",
        "mailnesia.com",
        "mintemail.com",
        "mohmal.com",
        "mytemp.email",
        "nada.email",
        "sharklasers.com",
        "spamgourmet.com",
        "temp-mail.io",
        "temp-mail.org",
        "tempail.com",
        "tempmail.com",
        "tempmail.dev",
        "tempmailo.com",
        "tempr.email",
        "throwawaymail.com",
        "trashmail.com",
        "yopmail.com"
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
        if (at <= 0 || at == address.Length - 1)
            return address;

        string localPart = address[..at];
        string domain = address[(at + 1)..];

        int plus = localPart.IndexOf('+');
        if (plus > 0)
            localPart = localPart[..plus];

        if (s_dotInsensitiveDomains.Contains(domain))
        {
            localPart = localPart.Replace(".", String.Empty);
            domain = "gmail.com";
        }

        return $"{localPart}@{domain}";
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
        if (at < 0 || at == address.Length - 1)
            return false;

        string domain = address[(at + 1)..];
        return blockedDomains.Any(blocked => domain == blocked || domain.EndsWith($".{blocked}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Trims, lowercases, removes a leading <c>@</c>, drops blanks and duplicates, and sorts a domain list for saving.
    /// </summary>
    public static List<string> NormalizeDomains(IEnumerable<string>? domains)
        => (domains ?? [])
            .Select(domain => domain.Trim().TrimStart('@').ToLowerInvariant())
            .Where(domain => domain.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
}
