using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class Token : IOwnedByOrganizationAndProjectWithIdentity, IHaveDates, IValidatableObject
{
    /// <summary>
    /// The user session generation captured when this token is issued.
    /// A legacy null value matches only an active user whose generation is also null; it does not describe a password format.
    /// </summary>
    public string? AuthenticationVersion { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The user's preferred default project. Cannot be set when ProjectId is defined.
    /// </summary>
    [ObjectId]
    public string? DefaultProjectId { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    [Required]
    public string Id { get; set; } = null!;

    public bool IsDisabled { get; set; }

    public bool IsSuspended { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Null for user-scoped tokens (where UserId is set instead).
    /// Required when ProjectId is set.
    /// </summary>
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// Null for organization-scoped or user-scoped tokens.
    /// Cannot be set together with UserId.
    /// </summary>
    [ObjectId]
    public string ProjectId { get; set; } = null!;

    public string? Refresh { get; set; }

    public HashSet<string> Scopes { get; set; } = new();

    public TokenType Type { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Set for user-scoped tokens. Cannot be set together with ProjectId.
    /// </summary>
    [ObjectId]
    public string? UserId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // OrganizationId is required for all non-user-scoped tokens.
        // Mirrors old FluentValidation: NotEmpty().When(!IsNullOrEmpty(ProjectId) || IsNullOrEmpty(UserId))
        // i.e. the only case that doesn't need OrganizationId is a pure user-scope token (UserId set, no ProjectId).
        if (String.IsNullOrEmpty(OrganizationId) && (!String.IsNullOrEmpty(ProjectId) || String.IsNullOrEmpty(UserId)))
        {
            yield return new ValidationResult("Please specify a valid organization id.", [nameof(OrganizationId)]);
        }

        if (CreatedUtc == DateTime.MinValue)
        {
            yield return new ValidationResult("Please specify a valid created date.", [nameof(CreatedUtc)]);
        }

        if (UpdatedUtc == DateTime.MinValue)
        {
            yield return new ValidationResult("Please specify a valid updated date.", [nameof(UpdatedUtc)]);
        }

        if (!String.IsNullOrEmpty(ProjectId) && !String.IsNullOrEmpty(DefaultProjectId))
        {
            yield return new ValidationResult("Default project id cannot be set when a project id is defined.", [nameof(DefaultProjectId)]);
        }

        if (!String.IsNullOrEmpty(ProjectId) && !String.IsNullOrEmpty(UserId))
        {
            yield return new ValidationResult("Can't set both user id and project id.", [nameof(UserId)]);
        }

        if (IsDisabled && Type != TokenType.Access)
        {
            yield return new ValidationResult("Only access tokens can be disabled", [nameof(IsDisabled)]);
        }
    }
}

public enum TokenType
{
    Authentication,
    Access
}
