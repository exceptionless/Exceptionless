using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ChangePasswordModel : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 6)]
    public string CurrentPassword { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (String.Equals(CurrentPassword, Password))
        {
            yield return new ValidationResult("New password must not be the same as the current password.", [nameof(Password)
            ]);
        }
    }
}
