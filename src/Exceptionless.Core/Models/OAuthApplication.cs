using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Services;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class OAuthApplication : IIdentity, IHaveDates, IValidatableObject
{
    public const string SystemUserId = "000000000000000000000001";

    [ObjectId]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(2048)]
    public string ClientId { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    [Length(0, 20)]
    public string[] RedirectUris { get; set; } = [];

    [Required]
    [Length(1, 20)]
    public string[] Scopes { get; set; } = [];

    [Required]
    [Length(1, 3)]
    public string[] GrantTypes { get; set; } = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken];

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
        foreach (string _ in GrantTypes.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("Grant type cannot be empty.", [nameof(GrantTypes)]);

        foreach (string grantType in GrantTypes.Where(g => !String.IsNullOrWhiteSpace(g)))
        {
            if (!OAuthGrantTypes.SupportedGrantTypes.Contains(grantType, StringComparer.Ordinal))
                yield return new ValidationResult($"'{grantType}' is not a supported OAuth grant type.", [nameof(GrantTypes)]);
        }

        bool supportsAuthorizationCode = GrantTypes.Contains(OAuthGrantTypes.AuthorizationCode, StringComparer.Ordinal);
        bool supportsDeviceCode = GrantTypes.Contains(OAuthGrantTypes.DeviceCode, StringComparer.Ordinal);
        if (!supportsAuthorizationCode && !supportsDeviceCode)
            yield return new ValidationResult("OAuth applications must support authorization_code or device_code.", [nameof(GrantTypes)]);

        if (GrantTypes.Contains(OAuthGrantTypes.RefreshToken, StringComparer.Ordinal) && !supportsAuthorizationCode && !supportsDeviceCode)
            yield return new ValidationResult("The refresh_token grant type requires authorization_code or device_code.", [nameof(GrantTypes)]);

        if (Scopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal) && !GrantTypes.Contains(OAuthGrantTypes.RefreshToken, StringComparer.Ordinal))
            yield return new ValidationResult("The offline_access scope requires the refresh_token grant type.", [nameof(Scopes)]);

        if (supportsAuthorizationCode && RedirectUris.Length == 0)
            yield return new ValidationResult("Redirect URIs are required for authorization_code clients.", [nameof(RedirectUris)]);

        foreach (string _ in RedirectUris.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("Redirect URI cannot be empty.", [nameof(RedirectUris)]);

        foreach (string redirectUri in RedirectUris.Where(uri => !String.IsNullOrWhiteSpace(uri)))
        {
            if (!IsValidRedirectUri(redirectUri))
                yield return new ValidationResult($"'{redirectUri}' must be an absolute HTTPS URI or loopback HTTP URI without a fragment.", [nameof(RedirectUris)]);
        }

        foreach (string _ in Scopes.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("Scope cannot be empty.", [nameof(Scopes)]);

        foreach (string scope in Scopes.Where(s => !String.IsNullOrWhiteSpace(s)))
        {
            if (!AllScopes.Contains(scope, StringComparer.Ordinal))
                yield return new ValidationResult($"'{scope}' is not a supported OAuth scope.", [nameof(Scopes)]);
        }
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
