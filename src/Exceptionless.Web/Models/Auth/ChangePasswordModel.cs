using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ChangePasswordModel : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 6)]
    public required string CurrentPassword { get; init; }

    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.Equals(CurrentPassword, Password))
        {
            yield return new ValidationResult("New password must not be the same as the current password.", [nameof(Password)
            ]);
        }
    }
}
