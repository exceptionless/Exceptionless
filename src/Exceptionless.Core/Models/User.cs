using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public record User : IIdentity, IHaveDates, IValidatableObject
{
    public bool? AssistantConversationSharingEnabled { get; set; }

    /// <summary>
    /// Session revocation generation, rotated when a password changes or resets, or an external login is removed or transferred away.
    /// Remains null until rotation; active users with a null generation accept only matching legacy null token generations.
    /// This value does not describe the password hashing format.
    /// </summary>
    public string? AuthenticationVersion { get; set; }

    [StringLength(2000)]
    public string? AvatarFileName { get; set; }

    public DateTime CreatedUtc { get; set; }

    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = null!;

    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the users Full Name.
    /// </summary>
    [Required]
    public string FullName { get; set; } = null!;

    /// <summary>
    /// Unique id that identifies an user.
    /// </summary>
    [ObjectId]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the users active state.
    /// </summary>
    public bool IsActive { get; init; } = true;

    public bool IsEmailAddressVerified { get; set; }

    public ICollection<OAuthAccount> OAuthAccounts { get; init; } = new Collection<OAuthAccount>();

    /// <summary>
    /// The organizations that the user has access to.
    /// </summary>
    public ISet<string> OrganizationIds { get; init; } = new HashSet<string>();

    public ICollection<UserOrganizationPreference> OrganizationPreferences { get; init; } = new Collection<UserOrganizationPreference>();

    public string? Password { get; set; }

    public string? PasswordResetToken { get; set; }

    public DateTime PasswordResetTokenExpiration { get; set; }

    public IDictionary<string, JsonElement> ProductTours { get; init; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public ISet<string> Roles { get; init; } = new HashSet<string>();

    public string? Salt { get; set; }

    public ICollection<UserSavedViewOrderPreference> SavedViewOrders { get; init; } = new Collection<UserSavedViewOrderPreference>();

    public DateTime UpdatedUtc { get; set; }

    public string? VerifyEmailAddressToken { get; set; }

    public DateTime VerifyEmailAddressTokenExpiration { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsEmailAddressVerified)
        {
            if (VerifyEmailAddressToken is not null)
            {
                yield return new ValidationResult("A verify email address token cannot be set if the email address has been verified.",
                    [nameof(VerifyEmailAddressToken)]);
            }

            if (VerifyEmailAddressTokenExpiration != default)
            {
                yield return new ValidationResult("A verify email address token expiration cannot be set if the email address has been verified.",
                    [nameof(VerifyEmailAddressTokenExpiration)]);
            }
        }
        else
        {
            if (String.IsNullOrWhiteSpace(VerifyEmailAddressToken))
            {
                yield return new ValidationResult("A verify email address token must be set if the email address has not been verified.",
                    [nameof(VerifyEmailAddressToken)]);
            }

            if (VerifyEmailAddressTokenExpiration == default)
            {
                yield return new ValidationResult("A verify email address token expiration must be set if the email address has not been verified.",
                    [nameof(VerifyEmailAddressTokenExpiration)]);
            }
        }
    }
}
