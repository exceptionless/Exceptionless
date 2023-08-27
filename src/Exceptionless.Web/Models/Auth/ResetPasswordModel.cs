namespace Exceptionless.Web.Models;

public record ResetPasswordModel
{
    public string? PasswordResetToken { get; set; }
    public string? Password { get; set; }
}
