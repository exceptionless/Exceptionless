namespace Exceptionless.Web.Models;

public record ChangePasswordModel
{
    public string? CurrentPassword { get; set; }
    public string? Password { get; set; }
}
