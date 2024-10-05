using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ResetPasswordModel
{
    [Required, StringLength(40, MinimumLength = 40)]
    public required string PasswordResetToken { get; init; }

    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }
}
