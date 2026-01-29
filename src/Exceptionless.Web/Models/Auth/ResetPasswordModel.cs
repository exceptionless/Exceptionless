using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public record ResetPasswordModel
{
    [Required, StringLength(40, MinimumLength = 40)]
    public string PasswordResetToken { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = null!;
}
