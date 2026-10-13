using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class OAuthToken : IIdentity, IHaveDates, IValidatableObject
{
    [Required]
    [MaxLength(100)]
    public string AccessTokenHash { get; set; } = null!;

    /// <summary>
    /// The user session generation captured when this OAuth token is issued.
    /// A legacy null value matches only an active user whose generation is also null; it does not describe a password format.
    /// </summary>
    public string? AuthenticationVersion { get; set; }

    [Required]
    [MaxLength(2048)]
    public string ClientId { get; set; } = null!;

    [Required]
    [ObjectId]
    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    [Required]
    [MaxLength(100)]
    public string GrantId { get; set; } = null!;

    [Required]
    [ObjectId]
    public string Id { get; set; } = null!;

    public bool IsDisabled { get; set; }

    public bool IsSuspended { get; set; }

    public HashSet<string> OrganizationIds { get; set; } = new(StringComparer.Ordinal);

    public DateTime? RefreshExpiresUtc { get; set; }

    [MaxLength(100)]
    public string? RefreshTokenHash { get; set; }

    public DateTime? RefreshTokenUsedUtc { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Resource { get; set; } = null!;

    public HashSet<string> Scopes { get; set; } = new(StringComparer.Ordinal);

    public DateTime UpdatedUtc { get; set; }

    [Required]
    [ObjectId]
    public string UserId { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreatedUtc == DateTime.MinValue)
            yield return new ValidationResult("Please specify a valid created date.", [nameof(CreatedUtc)]);

        if (UpdatedUtc == DateTime.MinValue)
            yield return new ValidationResult("Please specify a valid updated date.", [nameof(UpdatedUtc)]);

        if (Scopes.Count == 0)
            yield return new ValidationResult("OAuth tokens must specify at least one scope.", [nameof(Scopes)]);

        foreach (string _ in Scopes.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("OAuth scope cannot be empty.", [nameof(Scopes)]);

        if (OrganizationIds.Count == 0)
            yield return new ValidationResult("OAuth tokens must specify at least one organization id.", [nameof(OrganizationIds)]);

        foreach (string _ in OrganizationIds.Where(String.IsNullOrWhiteSpace))
            yield return new ValidationResult("OAuth organization id cannot be empty.", [nameof(OrganizationIds)]);
    }
}
