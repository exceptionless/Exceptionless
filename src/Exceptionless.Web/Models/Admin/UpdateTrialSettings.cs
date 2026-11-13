using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Exceptionless.Web.Models.Admin;

public sealed partial record UpdateTrialSettings : IValidatableObject
{
    /// <summary>
    /// Email domains that can't start a free trial. Subdomains are blocked too. Null restores the built-in list.
    /// </summary>
    [MaxLength(10000)]
    public IReadOnlyList<string>? BlockedEmailDomains { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BlockedEmailDomains?.Any(domain => domain is null || !DomainRegex().IsMatch(domain.Trim().TrimStart('@'))) is true)
        {
            yield return new ValidationResult(
                "Every blocked email domain must be a domain name, such as example.com.",
                [nameof(BlockedEmailDomains)]
            );
        }
    }

    [GeneratedRegex(@"^(?=.{1,253}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z0-9-]{2,63}$")]
    private static partial Regex DomainRegex();
}

public sealed record TrialSettings(IReadOnlyList<string> BlockedEmailDomains, IReadOnlyList<string> DefaultBlockedEmailDomains, bool IsDefault);
