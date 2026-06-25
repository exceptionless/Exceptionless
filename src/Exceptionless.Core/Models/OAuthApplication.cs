using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Authorization;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class OAuthApplication : IIdentity, IHaveDates, IValidatableObject
{
    public const string SystemUserId = "000000000000000000000000";

    [ObjectId]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(2048)]
    public string ClientId { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [Length(1, 20)]
    public string[] RedirectUris { get; set; } = [];

    [Required]
    [Length(1, 20)]
    public string[] Scopes { get; set; } = [];

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsDisabled { get; set; }

    [ObjectId]
    public string CreatedByUserId { get; set; } = null!;

    [ObjectId]
    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (string _ in RedirectUris.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("Redirect URI cannot be empty.", [nameof(RedirectUris)]);

        foreach (string redirectUri in RedirectUris.Where(uri => !String.IsNullOrWhiteSpace(uri)).Where(uri => !IsValidRedirectUri(uri)))
            yield return new ValidationResult($"'{redirectUri}' must be an absolute HTTPS URI or loopback HTTP URI without a fragment.", [nameof(RedirectUris)]);

        foreach (string _ in Scopes.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("Scope cannot be empty.", [nameof(Scopes)]);

        foreach (string scope in Scopes.Where(s => !String.IsNullOrWhiteSpace(s)).Where(scope => !AllScopes.Contains(scope, StringComparer.Ordinal)))
            yield return new ValidationResult($"'{scope}' is not a supported OAuth scope.", [nameof(Scopes)]);
    }

    public static readonly IReadOnlyCollection<string> AllScopes =
    [
        AuthorizationRoles.McpRead,
        AuthorizationRoles.ProjectsRead,
        AuthorizationRoles.StacksRead,
        AuthorizationRoles.StacksWrite,
        AuthorizationRoles.EventsRead,
        AuthorizationRoles.OfflineAccess
    ];

    public static bool IsValidRedirectUri(string redirectUri)
    {
        if (String.IsNullOrWhiteSpace(redirectUri) || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || !String.IsNullOrEmpty(uri.Fragment))
            return false;

        if (String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        return String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && (uri.IsLoopback || String.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }
}
