using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public record User : IIdentity, IHaveDates, IValidatableObject
{
    /// <summary>
    /// Unique id that identifies an user.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The organizations that the user has access to.
    /// </summary>
    public ICollection<string> OrganizationIds { get; } = new Collection<string>();

    public string? Password { get; set; }
    public string? Salt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime PasswordResetTokenExpiration { get; set; }
    public ICollection<OAuthAccount> OAuthAccounts { get; } = new Collection<OAuthAccount>();

    /// <summary>
    /// Gets or sets the users Full Name.
    /// </summary>
    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = null!;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool IsEmailAddressVerified { get; set; }
    public string? VerifyEmailAddressToken { get; set; }
    public DateTime VerifyEmailAddressTokenExpiration { get; set; }

    /// <summary>
    /// Gets or sets the users active state.
    /// </summary>
    public bool IsActive { get; init; } = true;

    public ICollection<string> Roles { get; init; } = new Collection<string>();

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

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
