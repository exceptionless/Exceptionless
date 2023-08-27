namespace Exceptionless.Web.Models;

public record UpdateUser
{
    public string? FullName { get; set; }
    public bool EmailNotificationsEnabled { get; set; }
}
